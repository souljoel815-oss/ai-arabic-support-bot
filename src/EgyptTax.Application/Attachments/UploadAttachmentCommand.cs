using EgyptTax.Domain.Workflow;

namespace EgyptTax.Application.Attachments;

/// <summary>
/// T139 / FR-032 — request to upload a binary attachment against
/// the parent document. Caller supplies the bytes via
/// <paramref name="ContentStream"/>; the handler hashes + persists
/// + creates the metadata row in one transaction.
///
/// Per FR-032: file types restricted to PDF / JPG / PNG, bounded
/// size (the handler validates), SHA-256 captured at upload time
/// for R-21 bit-rot detection.
/// </summary>
public sealed record UploadAttachmentCommand(
    Guid DocumentId,
    DocumentType DocumentType,
    string FilenameOriginal,
    string MimeType,
    Stream ContentStream,
    Guid UploadedByUserId);
