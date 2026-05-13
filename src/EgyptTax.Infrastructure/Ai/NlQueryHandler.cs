using System.Text.Json;
using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Settings;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// M.2 (v3 roadmap) — Arabic-NL query handler. Calls Claude with a
/// tight system prompt that lists DaftarX's pages + filters and
/// asks the model to:
///   (a) reply in 1-2 short Arabic sentences
///   (b) optionally suggest a page+filter URL the operator can open
///
/// Deliberately NOT a financial calculator. Claude is told NOT to
/// quote money figures or counts — the system prompt is explicit
/// about this. Reasoning: an LLM that confidently invents
/// "your tax is 4,250 EGP" when the real number is 8,250 is a
/// trust-killer. Routing-only is honest about what the model can
/// do without real-time data access.
///
/// Future v4: pre-fetch summary data into the prompt for specific
/// question patterns (e.g. on "كام ضريبتي" the handler runs the
/// monthly-VAT query first + injects the number). Out of scope
/// for v1.
/// </summary>
public sealed class NlQueryHandler
{
    private const string SystemPrompt = """
        أنت مساعد دفترخ — برنامج محاسبة مصري. مهمتك:
        تساعد المستخدم يلاقي المعلومة اللي عايزها بأسرع طريقة بإنه
        يفتح الصفحة الصح. مش بتحسب أرقام — بترشد المستخدم لمكانها.

        قواعد صارمة:
        - رد بـ JSON فقط (مش markdown، مش شرح، مش ```fences```).
        - في الـ JSON: حقل "reply" (نص عربي قصير، جملة أو اتنين) +
          اختيارياً "open_url" + "open_label" لو فيه صفحة مناسبة.
        - مش بتقول أرقام مالية أو إحصاءات (زي "ضريبتك 4,250 ج.م")
          لأنك مش شايف البيانات الحقيقية. بدل ما تقول رقم، قول
          "افتح صفحة الإقرار الشهري لتشوف الرقم الفعلي".
        - لو السؤال مش متعلق بالمحاسبة، قول إنك مساعد محاسبي بس.

        الصفحات المتاحة (URLs + الغرض):
        - / : لوحة التحكم (نظرة عامة + ضريبة الشهر + Penalty Shield)
        - /invoices : فواتير المبيعات (قائمة كاملة، فيها فلتر بالحالة)
        - /invoices?state=Draft : المسودات اللي محتاجة تترحل
        - /invoices/new : إنشاء فاتورة جديدة
        - /quotations : عروض الأسعار (Draft → Sent → Accepted → فاتورة)
        - /customers : قائمة العملاء
        - /customers/{id}/statement : كشف حساب عميل (مع التقادم)
        - /items : الأصناف + المخزون
        - /purchases : فواتير الموردين
        - /expenses : المصروفات
        - /payments : إيصالات القبض + سندات الصرف
        - /reports/vat-monthly : إقرار الضريبة الشهري
        - /reports/taxable-income : الدخل الخاضع لضريبة الدخل
        - /reports/trial-balance : ميزان المراجعة
        - /reports/sales-by-rep : مبيعات المناديب
        - /reports/commissions : عمولات المناديب
        - /compliance/penalty-shield : درع الغرامات (المخاطر + الإصلاحات)
        - /compliance/inspection : ملف الفحص (للتقديم لمأمور الضرائب)
        - /eta-dashboard : حالة منظومة ETA
        - /wht : دورة الخصم
        - /scan-receipt : مسح إيصال بالذكاء الاصطناعي
        - /data-import : استيراد بيانات من CSV
        - /backup (settings tab) : النسخ الاحتياطي
        - /settings : كل الإعدادات

        شكل الـ JSON المطلوب:
        {
          "reply": "هتلاقي ضريبة الشهر دي في صفحة الإقرار الشهري — افتحها من الزرار اللي تحت.",
          "open_url": "/reports/vat-monthly",
          "open_label": "افتح إقرار الشهر"
        }

        لو السؤال مش يستحق فتح صفحة، خلي open_url + open_label = null.
        """;

    private readonly SettingsRepository _settings;
    private readonly AnthropicApiKeyProtector _protector;
    private readonly AnthropicVisionClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IClock _clock;
    private readonly ILogger<NlQueryHandler> _log;

    public NlQueryHandler(
        SettingsRepository settings,
        AnthropicApiKeyProtector protector,
        AnthropicVisionClient client,
        IDbContextFactory<AppDbContext> dbFactory,
        IClock clock,
        ILogger<NlQueryHandler> log)
    {
        _settings = settings;
        _protector = protector;
        _client = client;
        _dbFactory = dbFactory;
        _clock = clock;
        _log = log;
    }

    public async Task<ChatResult> AskAsync(
        string question,
        Guid? userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var ai = await _settings.GetAiSettingsAsync(ct);
        if (!ai.Enabled || string.IsNullOrWhiteSpace(ai.EncryptedApiKey))
        {
            return ChatResult.Failure(
                "AI features are disabled. Configure the Anthropic API key in Settings → AI first.");
        }

        AnthropicVisionClient.VisionResult? raw = null;
        Parsed? parsed = null;
        string? errorMessage = null;

        try
        {
            var apiKey = _protector.Decrypt(ai.EncryptedApiKey);
            raw = await _client.SendChatMessageAsync(
                apiKey: apiKey,
                modelName: ai.ModelName,
                systemPrompt: SystemPrompt,
                userMessage: question,
                maxTokens: 400,
                ct: ct);
            parsed = TryParse(raw.AssistantText);
            if (parsed is null)
            {
                errorMessage = "Claude returned text that didn't match the expected JSON shape.";
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "NL query failed for question: {Question}", question);
            errorMessage = ex.Message;
        }

        await using (var db = await _dbFactory.CreateDbContextAsync(ct))
        {
            db.Add(new AiChatLog(
                askedAtUtc: _clock.UtcNow,
                askedByUserId: userId,
                question: question,
                rawResponseJson: raw?.RawResponseJson ?? "",
                reply: parsed?.Reply,
                openUrl: parsed?.OpenUrl,
                openLabel: parsed?.OpenLabel,
                inputTokens: raw?.InputTokens ?? 0,
                outputTokens: raw?.OutputTokens ?? 0,
                errorMessage: errorMessage));
            await db.SaveChangesAsync(ct);
        }

        if (errorMessage is not null || parsed is null)
        {
            return ChatResult.Failure(errorMessage ?? "Failed to parse reply.");
        }

        return ChatResult.Success(
            reply: parsed.Reply ?? "",
            openUrl: parsed.OpenUrl,
            openLabel: parsed.OpenLabel,
            inputTokens: raw!.InputTokens,
            outputTokens: raw.OutputTokens);
    }

    private static Parsed? TryParse(string assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText)) return null;

        var trimmed = assistantText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0) trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
                trimmed = trimmed[..^3].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            string? reply = root.TryGetProperty("reply", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString()
                : null;
            string? openUrl = root.TryGetProperty("open_url", out var u) && u.ValueKind == JsonValueKind.String
                ? u.GetString()
                : null;
            string? openLabel = root.TryGetProperty("open_label", out var l) && l.ValueKind == JsonValueKind.String
                ? l.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(reply)) return null;
            // Defense-in-depth: only allow same-origin relative paths.
            // If Claude hallucinates an absolute URL we drop it.
            if (openUrl is not null && !openUrl.StartsWith('/'))
                openUrl = null;
            return new Parsed(reply, openUrl, openLabel);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Parsed(string? Reply, string? OpenUrl, string? OpenLabel);
}

public sealed record ChatResult(
    bool Ok,
    string? ErrorMessage,
    string? Reply,
    string? OpenUrl,
    string? OpenLabel,
    int InputTokens,
    int OutputTokens)
{
    public static ChatResult Failure(string message) =>
        new(false, message, null, null, null, 0, 0);

    public static ChatResult Success(string reply, string? openUrl, string? openLabel,
        int inputTokens, int outputTokens) =>
        new(true, null, reply, openUrl, openLabel, inputTokens, outputTokens);
}
