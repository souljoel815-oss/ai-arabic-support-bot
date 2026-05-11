namespace EgyptTax.Application.Ocr;

/// <summary>
/// G3.2 — OCR engine port. The expense-form camera flow calls into
/// the implementation, which runs the OCR engine on the supplied
/// image bytes and returns extracted fields via
/// <see cref="ReceiptOcrExtractor"/>.
///
/// Returns <see cref="ReceiptOcrResult.Unavailable"/> when the
/// engine isn't installed on this box (Tesseract native libs +
/// tessdata language packs are optional — operator can ship without
/// them and the rest of the app keeps working).
/// </summary>
public interface IReceiptOcrService
{
    Task<ReceiptOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}

public sealed record ReceiptOcrResult(
    bool Available,
    ReceiptDraft? Draft,
    string? RawText,
    string? UnavailableReason)
{
    public static ReceiptOcrResult Unavailable(string reason) =>
        new(false, null, null, reason);

    public static ReceiptOcrResult Success(ReceiptDraft draft, string rawText) =>
        new(true, draft, rawText, null);
}
