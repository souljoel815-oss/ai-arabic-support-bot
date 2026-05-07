using EgyptTax.Domain.Workflow;

namespace EgyptTax.Domain.Documents;

/// <summary>
/// J1 / FR-032 — file attachment to a tax-impacting document. The
/// SHA-256 digest is captured at upload time so R-21's bit-rot
/// detection can verify the file on disk hasn't been altered or
/// corrupted post-upload. Bytes themselves live on the filesystem
/// (per R-21) under <see cref="RelativePath"/>; this entity holds
/// the metadata only.
///
/// FR-027 invariant: once the parent document is Posted, the row
/// cannot be deleted. The aggregate enforces this by exposing only
/// init-only state — the application-layer guard is the
/// `BlockDeleteOfReferencedMasterData`-style policy that lands in
/// T140.
/// </summary>
public sealed class Attachment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DocumentId { get; init; }
    public DocumentType DocumentType { get; init; }
    public string FilenameOriginal { get; init; } = "";
    public string FilenameStorage { get; init; } = "";
    public string RelativePath { get; init; } = "";
    public byte[] Sha256 { get; init; } = Array.Empty<byte>();
    public string MimeType { get; init; } = "";
    public long SizeBytes { get; init; }
    public Guid UploadedByUserId { get; init; }
    public DateTime UploadedAtUtc { get; init; }

    private Attachment() { }

    public Attachment(
        Guid documentId,
        DocumentType documentType,
        string filenameOriginal,
        string filenameStorage,
        string relativePath,
        byte[] sha256,
        string mimeType,
        long sizeBytes,
        Guid uploadedByUserId,
        DateTime uploadedAtUtc)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("DocumentId is required.", nameof(documentId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(filenameOriginal);
        ArgumentException.ThrowIfNullOrWhiteSpace(filenameStorage);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentNullException.ThrowIfNull(sha256);
        if (sha256.Length != 32)
        {
            throw new ArgumentException(
                "SHA-256 digest must be exactly 32 bytes.", nameof(sha256));
        }
        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes),
                "Attachment size must be positive.");
        }

        DocumentId = documentId;
        DocumentType = documentType;
        FilenameOriginal = filenameOriginal;
        FilenameStorage = filenameStorage;
        RelativePath = relativePath;
        Sha256 = sha256;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc;
    }
}
