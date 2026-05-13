using System.Globalization;
using System.Text.Json;
using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Settings;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// M.1 (v3 roadmap) — orchestrates a single receipt-OCR call:
///   1. Read AiSettings (decrypts the API key)
///   2. Call Claude Vision with the Arabic extraction prompt
///   3. Parse the JSON the assistant returns into typed fields
///   4. Persist a ReceiptScan row (audit + future fine-tune corpus)
///   5. Return the parsed result for the page to display
///
/// Failure modes (each surfaced as an OcrResult with .Success=false):
///   - AI not enabled / no API key configured
///   - HTTP error from Anthropic (rate limit, bad key, model down)
///   - Claude returns text that's not valid JSON
///   - JSON missing required fields
///
/// In the failure cases we still write a ReceiptScan row with the
/// raw response + ErrorMessage so the operator can debug + we can
/// see usage even on failed calls.
/// </summary>
public sealed class OcrReceiptHandler
{
    private const string ExtractionPrompt = """
        أنت مساعد محاسبي عربي. اقرأ الإيصال أو الفاتورة المرفقة واستخرج
        البيانات التالية فقط — رد بـ JSON صالح بدون أي شرح إضافي:

        {
          "vendor": "اسم البائع كما يظهر في الإيصال",
          "date": "YYYY-MM-DD (تاريخ الإيصال)",
          "total_egp": 1234.56,
          "vat_egp": 156.00,
          "category": "واحدة من: travel, fuel, office, food, telecom, other"
        }

        قواعد:
        - لو حاجة مش واضحة، حط null في خانتها (مش فاضية).
        - الأرقام لازم تكون decimals بدون فواصل آلاف.
        - التاريخ بصيغة ISO (YYYY-MM-DD). لو السنة مش مكتوبة، استخدم السنة الحالية.
        - رد بـ JSON نقي فقط، بدون ```markdown``` أو شرح.
        """;

    private readonly SettingsRepository _settings;
    private readonly AnthropicApiKeyProtector _protector;
    private readonly AnthropicVisionClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IClock _clock;
    private readonly ILogger<OcrReceiptHandler> _log;

    public OcrReceiptHandler(
        SettingsRepository settings,
        AnthropicApiKeyProtector protector,
        AnthropicVisionClient client,
        IDbContextFactory<AppDbContext> dbFactory,
        IClock clock,
        ILogger<OcrReceiptHandler> log)
    {
        _settings = settings;
        _protector = protector;
        _client = client;
        _dbFactory = dbFactory;
        _clock = clock;
        _log = log;
    }

    public async Task<OcrResult> ScanAsync(
        string fileName,
        string mimeType,
        byte[] imageBytes,
        Guid? userId,
        CancellationToken ct = default)
    {
        var ai = await _settings.GetAiSettingsAsync(ct);
        if (!ai.Enabled || string.IsNullOrWhiteSpace(ai.EncryptedApiKey))
        {
            return OcrResult.Failure(
                "AI features are disabled. Configure the Anthropic API key in Settings → AI first.");
        }

        AnthropicVisionClient.VisionResult? rawResult = null;
        Extracted? extracted = null;
        string? errorMessage = null;

        try
        {
            var apiKey = _protector.Decrypt(ai.EncryptedApiKey);
            rawResult = await _client.SendVisionMessageAsync(
                apiKey: apiKey,
                modelName: ai.ModelName,
                imageBytes: imageBytes,
                imageMimeType: mimeType,
                textPrompt: ExtractionPrompt,
                maxTokens: 1024,
                ct: ct);

            extracted = TryParseExtraction(rawResult.AssistantText);
            if (extracted is null)
            {
                errorMessage = "Claude returned text that didn't match the expected JSON shape.";
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Receipt OCR failed for {File}", fileName);
            errorMessage = ex.Message;
        }

        // Always log — success and failure both. The audit row is
        // valuable for debugging + for the future fine-tune corpus.
        await using (var db = await _dbFactory.CreateDbContextAsync(ct))
        {
            db.Add(new ReceiptScan(
                scannedAtUtc: _clock.UtcNow,
                scannedByUserId: userId,
                fileName: fileName,
                mimeType: mimeType,
                fileSizeBytes: imageBytes.LongLength,
                rawResponseJson: rawResult?.RawResponseJson ?? "",
                extractedVendor: extracted?.Vendor,
                extractedDate: extracted?.Date,
                extractedTotalEgp: extracted?.TotalEgp,
                extractedVatEgp: extracted?.VatEgp,
                extractedCategory: extracted?.Category,
                inputTokens: rawResult?.InputTokens ?? 0,
                outputTokens: rawResult?.OutputTokens ?? 0,
                errorMessage: errorMessage));
            await db.SaveChangesAsync(ct);
        }

        if (errorMessage is not null || extracted is null)
        {
            return OcrResult.Failure(errorMessage ?? "Failed to extract data.");
        }

        return OcrResult.Success(
            vendor: extracted.Vendor,
            date: extracted.Date,
            totalEgp: extracted.TotalEgp,
            vatEgp: extracted.VatEgp,
            category: extracted.Category,
            inputTokens: rawResult!.InputTokens,
            outputTokens: rawResult.OutputTokens);
    }

    private static Extracted? TryParseExtraction(string assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText)) return null;

        // Tolerate the model wrapping its JSON in ```json fences``` —
        // we asked it not to but Haiku occasionally does anyway.
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

            string? vendor = root.TryGetProperty("vendor", out var vp) && vp.ValueKind == JsonValueKind.String
                ? vp.GetString()
                : null;

            DateOnly? date = null;
            if (root.TryGetProperty("date", out var dp) && dp.ValueKind == JsonValueKind.String)
            {
                if (DateOnly.TryParseExact(dp.GetString() ?? "", "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    date = d;
                }
            }

            decimal? total = TryGetDecimal(root, "total_egp");
            decimal? vat = TryGetDecimal(root, "vat_egp");

            string? category = root.TryGetProperty("category", out var cp) && cp.ValueKind == JsonValueKind.String
                ? cp.GetString()
                : null;

            // Need at least one populated field to call this a successful parse.
            if (vendor is null && date is null && total is null && vat is null && category is null)
                return null;

            return new Extracted(vendor, date, total, vat, category);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static decimal? TryGetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(p.GetString(),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private sealed record Extracted(
        string? Vendor,
        DateOnly? Date,
        decimal? TotalEgp,
        decimal? VatEgp,
        string? Category);
}

public sealed record OcrResult(
    bool Ok,
    string? ErrorMessage,
    string? Vendor,
    DateOnly? Date,
    decimal? TotalEgp,
    decimal? VatEgp,
    string? Category,
    int InputTokens,
    int OutputTokens)
{
    public static OcrResult Failure(string message) =>
        new(false, message, null, null, null, null, null, 0, 0);

    public static OcrResult Success(
        string? vendor, DateOnly? date, decimal? totalEgp, decimal? vatEgp,
        string? category, int inputTokens, int outputTokens) =>
        new(true, null, vendor, date, totalEgp, vatEgp, category, inputTokens, outputTokens);
}
