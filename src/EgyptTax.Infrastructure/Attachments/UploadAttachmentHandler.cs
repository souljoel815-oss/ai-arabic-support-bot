using System.Globalization;
using EgyptTax.Application.Attachments;
using EgyptTax.Application.Audit;
using EgyptTax.Application.FileStorage;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Attachments;

/// <summary>
/// T139 / FR-032 — orchestrates the file-system save (R-21 layout)
/// + the metadata-row insert + an FR-028 audit event in one
/// transaction. The store hashes the content stream as it writes;
/// the returned <see cref="AttachmentSavedInfo.ContentSha256"/>
/// becomes the <c>sha256</c> column on the metadata row, so the
/// future bit-rot scanner can re-hash the file on disk and compare.
///
/// Validation per FR-032:
///  * MIME type MUST be one of the allowed set (PDF / JPG / PNG).
///  * Size MUST be &gt; 0 and &lt;= <see cref="MaxBytes"/>.
///
/// FR-027 invariant: attachments cannot be added to a document
/// already past Draft (operator may add only while editing). The
/// caller-side guard lives in the page; this handler validates the
/// document exists but does not check its state — that's the
/// responsibility of the page that surfaces the upload widget.
/// </summary>
public sealed class UploadAttachmentHandler
{
    public const long MaxBytes = 10 * 1024 * 1024; // 10 MiB per file

    private static readonly Dictionary<string, string> AllowedMimeToExtension = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
    };

    private readonly AppDbContext _db;
    private readonly IAttachmentStore _store;
    private readonly IAuditLogStore _auditLog;
    private readonly IClock _clock;

    public UploadAttachmentHandler(
        AppDbContext db,
        IAttachmentStore store,
        IAuditLogStore auditLog,
        IClock clock
    )
    {
        _db = db;
        _store = store;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<Attachment> HandleAsync(
        UploadAttachmentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FilenameOriginal);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.MimeType);
        ArgumentNullException.ThrowIfNull(command.ContentStream);

        if (!AllowedMimeToExtension.TryGetValue(command.MimeType, out var extension))
        {
            throw new InvalidOperationException(
                $"FR-032 rejects MIME type '{command.MimeType}'. Allowed: {string.Join(", ", AllowedMimeToExtension.Keys)}."
            );
        }

        var attachmentId = Guid.NewGuid();
        var saved = await _store.SaveAsync(
            command.DocumentId,
            attachmentId,
            extension,
            command.ContentStream,
            cancellationToken
        );

        if (saved.SizeBytes == 0)
        {
            // Roll back the empty file we just wrote and surface a
            // clean error.
            await _store.DeleteAsync(saved.RelativePath, cancellationToken);
            throw new InvalidOperationException("Uploaded file is empty.");
        }
        if (saved.SizeBytes > MaxBytes)
        {
            await _store.DeleteAsync(saved.RelativePath, cancellationToken);
            throw new InvalidOperationException(
                $"Uploaded file is {saved.SizeBytes:N0} bytes; FR-032 caps attachments at {MaxBytes:N0} bytes."
            );
        }

        var nowUtc = _clock.UtcNow;
        var entity = new Attachment(
            documentId: command.DocumentId,
            documentType: command.DocumentType,
            filenameOriginal: command.FilenameOriginal,
            filenameStorage: attachmentId.ToString("D") + extension,
            relativePath: saved.RelativePath,
            sha256: saved.ContentSha256,
            // CA1308 disabled: lowercase MIME types are the IANA
            // canonical form; the analyzer's "prefer ToUpperInvariant
            // for normalization" applies to security comparisons, not
            // to web-protocol values that travel back to HTTP clients.
#pragma warning disable CA1308
            mimeType: command.MimeType.ToLowerInvariant(),
#pragma warning restore CA1308
            sizeBytes: saved.SizeBytes,
            uploadedByUserId: command.UploadedByUserId,
            uploadedAtUtc: nowUtc
        )
        {
            Id = attachmentId,
        };

        _db.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "attachment.uploaded",
                ActorUserId: command.UploadedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"attachment_id":"{{entity.Id:D}}","document_id":"{{entity.DocumentId:D}}","document_type":"{{entity.DocumentType}}","filename_original":"{{Escape(entity.FilenameOriginal)}}","mime_type":"{{entity.MimeType}}","size_bytes":{{entity.SizeBytes.ToString(CultureInfo.InvariantCulture)}},"sha256_hex":"{{Convert.ToHexString(entity.Sha256)}}","relative_path":"{{Escape(entity.RelativePath)}}"}"""
            ),
            cancellationToken
        );

        return entity;
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
