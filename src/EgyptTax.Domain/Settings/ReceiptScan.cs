namespace EgyptTax.Domain.Settings;

/// <summary>
/// M.1 (v3 roadmap) — append-only audit log of every Claude
/// Vision OCR call. Records:
///   - the original file (size + mime; bytes intentionally NOT
///     stored — the operator's original PDF/image lives in their
///     filesystem; we'd just bloat daftarx.db)
///   - Claude's raw JSON response (string column) for traceability
///     when the operator asks "why did it suggest 850 EGP, the
///     receipt clearly says 950"
///   - parsed structured fields (vendor / amount / etc) extracted
///     by the OCR handler
///   - input + output token counts so the running cost is visible
///   - which user triggered it
///
/// Becomes the seed corpus for fine-tuning a custom model later
/// once we have ~1000 real receipt scans across customers.
/// </summary>
public sealed class ReceiptScan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime ScannedAtUtc { get; init; }
    public Guid? ScannedByUserId { get; init; }

    public string FileName { get; init; } = "";
    public string MimeType { get; init; } = "";
    public long FileSizeBytes { get; init; }

    /// <summary>The full claude.messages response JSON. Capped at
    /// 64 KB; longer responses get truncated with a marker.</summary>
    public string RawResponseJson { get; init; } = "";

    /// <summary>Parsed by OcrReceiptHandler. Null if Claude returned
    /// non-JSON or the parse failed; RawResponseJson still holds the
    /// raw output for debugging.</summary>
    public string? ExtractedVendor { get; init; }
    public DateOnly? ExtractedDate { get; init; }
    public decimal? ExtractedTotalEgp { get; init; }
    public decimal? ExtractedVatEgp { get; init; }
    public string? ExtractedCategory { get; init; }

    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }

    /// <summary>Set when Claude's response was malformed / didn't
    /// match the expected JSON schema. Operator sees this in the
    /// scan log to understand why the form wasn't pre-filled.</summary>
    public string? ErrorMessage { get; init; }

    private ReceiptScan() { }

    public ReceiptScan(
        DateTime scannedAtUtc,
        Guid? scannedByUserId,
        string fileName,
        string mimeType,
        long fileSizeBytes,
        string rawResponseJson,
        string? extractedVendor,
        DateOnly? extractedDate,
        decimal? extractedTotalEgp,
        decimal? extractedVatEgp,
        string? extractedCategory,
        int inputTokens,
        int outputTokens,
        string? errorMessage = null)
    {
        ScannedAtUtc = scannedAtUtc;
        ScannedByUserId = scannedByUserId;
        FileName = fileName ?? "";
        MimeType = mimeType ?? "";
        FileSizeBytes = fileSizeBytes;
        RawResponseJson = rawResponseJson?.Length > 3_900
            ? rawResponseJson[..3_900] + "…[truncated]"
            : rawResponseJson ?? "";
        ExtractedVendor = extractedVendor;
        ExtractedDate = extractedDate;
        ExtractedTotalEgp = extractedTotalEgp;
        ExtractedVatEgp = extractedVatEgp;
        ExtractedCategory = extractedCategory;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        ErrorMessage = errorMessage;
    }
}
