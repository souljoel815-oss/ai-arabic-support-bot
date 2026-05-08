namespace EgyptTax.Application.FileStorage;

/// <summary>
/// R-21 — port over binary attachment storage. Implementations write
/// to a content store keyed by (document_id, attachment_id, extension)
/// and return the relative path the attachment metadata row should
/// reference. The path is the *only* handle subsequent reads /
/// deletes need; callers do not have to reconstruct the date-based
/// folder layout themselves.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>
    /// Persist <paramref name="content"/> at the canonical R-21 path
    /// for (<paramref name="documentId"/>, <paramref name="attachmentId"/>,
    /// <paramref name="fileExtension"/>). Overwrites any existing file
    /// at that path so retried saves are idempotent. Returns the
    /// relative path, byte count, and SHA-256 content hash; the
    /// metadata table stores all three.
    /// </summary>
    Task<AttachmentSavedInfo> SaveAsync(
        Guid documentId,
        Guid attachmentId,
        string fileExtension,
        Stream content,
        CancellationToken cancellationToken = default
    );

    /// <summary>Open the attachment for reading. Throws when missing.</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Idempotent — does not throw when the file is already absent.</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    bool Exists(string relativePath);
}

/// <summary>
/// What the store returns from <see cref="IAttachmentStore.SaveAsync"/>.
/// </summary>
public sealed record AttachmentSavedInfo(string RelativePath, long SizeBytes, byte[] ContentSha256);
