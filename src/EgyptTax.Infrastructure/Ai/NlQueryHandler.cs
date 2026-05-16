using System.Text.Json;
using EgyptTax.Domain.Identity;
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
        - الأرقام: لو تحت قسم "لقطة الكتب الحالية" فيه رقم بيرد على
          سؤال المستخدم، اقتبسه حرفياً (مثلاً "عندك 7 فواتير مسودة").
          لو الرقم مش موجود في اللقطة، عندك أدوات (tools) تقدر تستدعيها
          علشان تجيب الرقم الحقيقي من قاعدة البيانات:
            * count_sales_invoices_by_state(state) — عدد فواتير المبيعات
              في حالة معينة.
            * vat_for_month(year, month) — ضريبة شهر معيّن (مخرجات،
              مدخلات، صافي).
            * top_sales_customers_this_month(limit) — أعلى العملاء
              مبيعاً هذا الشهر.
          استخدم الأدوات لما السؤال محتاج رقم مش في اللقطة. متخمنش
          أرقام ولا تحسب عمليات حسابية بنفسك.
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
    private readonly GroqChatClient _groq;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IClock _clock;
    private readonly AiContextSnapshotProvider _snapshot;
    private readonly AiToolbox _toolbox;
    private readonly ILogger<NlQueryHandler> _log;

    public NlQueryHandler(
        SettingsRepository settings,
        AnthropicApiKeyProtector protector,
        AnthropicVisionClient client,
        GroqChatClient groq,
        IDbContextFactory<AppDbContext> dbFactory,
        IClock clock,
        AiContextSnapshotProvider snapshot,
        AiToolbox toolbox,
        ILogger<NlQueryHandler> log)
    {
        _settings = settings;
        _protector = protector;
        _client = client;
        _groq = groq;
        _dbFactory = dbFactory;
        _clock = clock;
        _snapshot = snapshot;
        _toolbox = toolbox;
        _log = log;
    }

    public async Task<ChatResult> AskAsync(
        string question,
        Guid? userId,
        string? currentUrl = null,
        string? userRoleHint = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var ai = await _settings.GetAiSettingsAsync(ct);
        if (!ai.Enabled)
        {
            return ChatResult.Failure(
                "AI features are disabled. Configure a chat provider in Settings → AI first.");
        }
        // v5 — route by ChatProvider. Each provider has its own
        // encrypted key column so swapping doesn't wipe the other.
        var providerKeyRaw = ai.ChatProvider == AiChatProvider.Groq
            ? ai.EncryptedGroqApiKey
            : ai.EncryptedApiKey;
        if (string.IsNullOrWhiteSpace(providerKeyRaw))
        {
            return ChatResult.Failure(
                $"AI chat is set to {ai.ChatProvider} but no API key is configured for it. Open Settings → AI.");
        }

        string assistantText = "";
        string rawJson = "";
        int inputTokens = 0;
        int outputTokens = 0;
        Parsed? parsed = null;
        string? errorMessage = null;

        // v5 — memory: pull last 6 successful turns for this user as
        // alternating user/assistant priors. Older turns get dropped
        // first so context stays bounded (~1.5k tokens worst case for
        // 6 turns of short replies).
        // v5 — awareness: enrich the system prompt with the operator's
        // current URL + role hint so the model can answer questions
        // like "where am I?" or skip routing if they're already on
        // the right page.
        var priorMessages = await LoadPriorMessagesAsync(userId, ct);
        var memoryNotes = await LoadMemoryNotesAsync(userId, ct);
        var snapshot = await _snapshot.BuildAsync(ct);
        var enrichedSystem = BuildEnrichedSystemPrompt(currentUrl, userRoleHint, memoryNotes, snapshot);

        try
        {
            var apiKey = _protector.Decrypt(providerKeyRaw);
            if (ai.ChatProvider == AiChatProvider.Groq)
            {
                var (finalText, finalJson, inT, outT) = await RunGroqWithToolsAsync(
                    apiKey, ai.GroqModelName, enrichedSystem, priorMessages, question, ct);
                assistantText = finalText;
                rawJson = finalJson;
                inputTokens = inT;
                outputTokens = outT;
            }
            else
            {
                var anthropicResult = await _client.SendChatMessageAsync(
                    apiKey: apiKey,
                    modelName: ai.ModelName,
                    systemPrompt: enrichedSystem,
                    userMessage: question,
                    maxTokens: 400,
                    ct: ct);
                assistantText = anthropicResult.AssistantText;
                rawJson = anthropicResult.RawResponseJson;
                inputTokens = anthropicResult.InputTokens;
                outputTokens = anthropicResult.OutputTokens;
            }
            parsed = TryParse(assistantText);
            if (parsed is null)
            {
                errorMessage = $"{ai.ChatProvider} returned text that didn't match the expected JSON shape.";
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
                rawResponseJson: rawJson,
                reply: parsed?.Reply,
                openUrl: parsed?.OpenUrl,
                openLabel: parsed?.OpenLabel,
                inputTokens: inputTokens,
                outputTokens: outputTokens,
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
            inputTokens: inputTokens,
            outputTokens: outputTokens);
    }

    /// <summary>v5 — Groq chat with tool-calling loop. Lets the model
    /// request <see cref="AiToolbox"/> functions (read-only DB queries)
    /// to answer questions the static snapshot doesn't cover. Loops up
    /// to 4 rounds before giving up — empirically 1-2 is enough for
    /// any single user question; 4 is a hard guard against runaway
    /// "model keeps requesting tools" pathology.</summary>
    private async Task<(string Text, string RawJson, int InTokens, int OutTokens)> RunGroqWithToolsAsync(
        string apiKey, string modelName, string systemPrompt,
        List<(string Role, string Content)> priorMessages, string userQuestion,
        CancellationToken ct)
    {
        var thread = new List<GroqChatClient.ToolThreadMessage>
        {
            new("system", systemPrompt),
        };
        foreach (var (role, content) in priorMessages)
        {
            if (string.IsNullOrWhiteSpace(content)) continue;
            thread.Add(new GroqChatClient.ToolThreadMessage(role, content));
        }
        thread.Add(new GroqChatClient.ToolThreadMessage("user", userQuestion));

        var tools = AiToolbox.GetToolDefinitions();
        int totalIn = 0, totalOut = 0;
        string lastRaw = "";

        const int MaxRounds = 4;
        for (int round = 0; round < MaxRounds; round++)
        {
            var resp = await _groq.SendChatThreadAsync(
                apiKey: apiKey,
                modelName: modelName,
                thread: thread,
                tools: tools,
                maxTokens: 600,
                ct: ct);
            totalIn += resp.InputTokens;
            totalOut += resp.OutputTokens;
            lastRaw = resp.RawResponseJson;

            if (resp.ToolCalls.Count == 0)
            {
                return (resp.AssistantText, lastRaw, totalIn, totalOut);
            }

            // Echo the assistant's tool-call message back into the
            // thread, then append a "tool" role message for each
            // call with its JSON result.
            var toolCallObjs = resp.ToolCalls.Select(tc => (object)new
            {
                id = tc.Id,
                type = "function",
                function = new { name = tc.FunctionName, arguments = tc.ArgumentsJson },
            }).ToArray();
            thread.Add(new GroqChatClient.ToolThreadMessage(
                Role: "assistant", Content: null, ToolCallId: null, ToolCalls: toolCallObjs));

            foreach (var call in resp.ToolCalls)
            {
                _log.LogInformation("AI tool call: {Tool}({Args})", call.FunctionName, call.ArgumentsJson);
                var result = await _toolbox.ExecuteAsync(call.FunctionName, call.ArgumentsJson, ct);
                thread.Add(new GroqChatClient.ToolThreadMessage(
                    Role: "tool", Content: result, ToolCallId: call.Id));
            }
        }

        _log.LogWarning("Groq tool loop hit MaxRounds={Max} without a final reply; returning last raw response.", MaxRounds);
        return ("", lastRaw, totalIn, totalOut);
    }

    /// <summary>v5 — pull the last N completed turns for this user
    /// from <c>AiChatLog</c> and shape them as (user, question) +
    /// (assistant, reply) pairs ordered oldest-first, so the model
    /// gets a coherent conversation thread instead of a one-shot
    /// question. Failed turns (with error_message set) are skipped
    /// so we don't poison the context with replies the model never
    /// actually produced.</summary>
    private async Task<List<(string Role, string Content)>> LoadPriorMessagesAsync(
        Guid? userId, CancellationToken ct)
    {
        if (userId is null) return new();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var recent = await db.Set<AiChatLog>().AsNoTracking()
            .Where(x => x.AskedByUserId == userId
                && x.ErrorMessage == null
                && x.Reply != null)
            .OrderByDescending(x => x.AskedAtUtc)
            .Take(6)
            .ToListAsync(ct);
        recent.Reverse(); // oldest-first for the message thread
        var prior = new List<(string Role, string Content)>(recent.Count * 2);
        foreach (var row in recent)
        {
            prior.Add(("user", row.Question));
            // Replies are stored as plain text; the original JSON
            // wrapper is in raw_response_json. Plain text is fine
            // for context — the model just needs to see what it said.
            prior.Add(("assistant", row.Reply ?? ""));
        }
        return prior;
    }

    /// <summary>v5 — extend <see cref="SystemPrompt"/> with the
    /// operator's current page + role + persistent "remember this
    /// about me" notes so the model can give context-aware answers
    /// (skip routing if they're on the right page, suggest
    /// role-appropriate next steps, recall user preferences).</summary>
    private static string BuildEnrichedSystemPrompt(
        string? currentUrl, string? userRoleHint, string? memoryNotes,
        AiContextSnapshot? snapshot)
    {
        var hasCtx = !string.IsNullOrWhiteSpace(currentUrl)
            || !string.IsNullOrWhiteSpace(userRoleHint);
        var hasNotes = !string.IsNullOrWhiteSpace(memoryNotes);
        var hasSnapshot = snapshot is not null && snapshot != AiContextSnapshot.Empty;
        if (!hasCtx && !hasNotes && !hasSnapshot) return SystemPrompt;

        var sb = new System.Text.StringBuilder(SystemPrompt);
        if (hasCtx)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("==== سياق المستخدم الحالي ====");
            if (!string.IsNullOrWhiteSpace(currentUrl))
            {
                sb.Append("- المستخدم حالياً في الصفحة: ").AppendLine(currentUrl);
            }
            if (!string.IsNullOrWhiteSpace(userRoleHint))
            {
                sb.Append("- دور المستخدم: ").AppendLine(userRoleHint);
            }
            sb.AppendLine("- لو السؤال متعلق بالصفحة الحالية، رد مباشرة بدل ما توجه لصفحة تانية.");
        }
        if (hasNotes)
        {
            sb.AppendLine();
            sb.AppendLine("==== ملاحظات المستخدم الشخصية (ذاكرة دائمة) ====");
            sb.AppendLine(memoryNotes!.Trim());
            sb.AppendLine("- استخدم الملاحظات دي علشان ترد بأسلوب مناسب لتفضيلات المستخدم ودوره.");
        }
        if (hasSnapshot)
        {
            var snap = snapshot!;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine();
            sb.AppendLine("==== لقطة الكتب الحالية (أرقام حقيقية من قاعدة البيانات) ====");
            sb.Append("- فواتير المبيعات المسودة: ").AppendLine(snap.DraftSalesCount.ToString("N0", inv));
            sb.Append("- فواتير المشتريات المسودة: ").AppendLine(snap.DraftPurchasesCount.ToString("N0", inv));
            sb.Append("- مبيعات الشهر (المرحّلة): ")
              .Append(snap.MonthSalesCount.ToString("N0", inv))
              .Append(" فاتورة بقيمة ")
              .Append(snap.MonthSalesTotalEgp.ToString("N2", inv))
              .AppendLine(" ج.م");
            sb.Append("- مشتريات الشهر (المرحّلة): ")
              .Append(snap.MonthPurchasesCount.ToString("N0", inv))
              .Append(" فاتورة بقيمة ")
              .Append(snap.MonthPurchasesTotalEgp.ToString("N2", inv))
              .AppendLine(" ج.م");
            sb.Append("- ضريبة المخرجات هذا الشهر: ").Append(snap.MonthVatOutputEgp.ToString("N2", inv)).AppendLine(" ج.م");
            sb.Append("- ضريبة المدخلات هذا الشهر: ").Append(snap.MonthVatInputEgp.ToString("N2", inv)).AppendLine(" ج.م");
            sb.Append("- صافي ض.ق.م (مخرجات - مدخلات): ").Append(snap.MonthVatNetEgp.ToString("N2", inv)).AppendLine(" ج.م");
            sb.AppendLine();
            sb.AppendLine("مهم: الأرقام دي مأخوذة مباشرة من قاعدة البيانات، اقتبسها كما هي لو السؤال عنها.");
            sb.AppendLine("لكن مش مسموح تحسب أرقام جديدة منها (مثلاً تخصم خصومات، تحسب ضريبة على مبلغ، تتنبأ بأرقام مش موجودة) — اطلب من المستخدم يفتح الصفحة المناسبة بدل الحساب.");
        }
        return sb.ToString();
    }

    /// <summary>v5 — pull the user's free-text "remember this about
    /// me" notes from the User row. Empty/whitespace returns null so
    /// the system prompt stays clean for users who haven't set
    /// notes yet.</summary>
    private async Task<string?> LoadMemoryNotesAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is null) return null;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var notes = await db.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.AiMemoryNotes)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(notes) ? null : notes;
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
