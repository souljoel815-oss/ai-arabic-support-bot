using System.Security.Cryptography;
using EgyptTax.Application.FileStorage;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.Infrastructure.FileStorage;

/// <summary>
/// R-21 — file-system-backed attachment store. Layout:
///   <c>{root}/attachments/{yyyy}/{mm}/{document_id}/{attachment_id}.{ext}</c>
/// The year/month folders allow operator runbooks (DR backup, retention
/// pruning) to scope work chronologically; the per-document folder
/// keeps every supporting file for one invoice/voucher together so
/// inspectors can ZIP a single directory. Operator setup grants the
/// application's service account the only write privilege on
/// <c>{root}/attachments</c> per the contract.
/// </summary>
public sealed class FileSystemAttachmentStore(string rootDirectory, IClock clock) : IAttachmentStore
{
    private const string AttachmentsFolder = "attachments";

    private readonly string _root = ResolveRoot(rootDirectory);
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<AttachmentSavedInfo> SaveAsync(
        Guid documentId,
        Guid attachmentId,
        string fileExtension,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var ext = NormalizeExtension(fileExtension);

        var nowUtc = _clock.UtcNow;
        var relative = string.Join('/', new[]
        {
            AttachmentsFolder,
            nowUtc.Year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
            nowUtc.Month.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
            documentId.ToString("D"),
            attachmentId.ToString("D") + ext,
        });

        var absolute = ResolveAbsolutePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        long sizeBytes;
        byte[] hash;
        await using (var fs = new FileStream(absolute, FileMode.Create, FileAccess.Write, FileShare.None,
                         bufferSize: 81920, useAsync: true))
        using (var sha = SHA256.Create())
        {
            using var hashing = new CryptoStream(fs, sha, CryptoStreamMode.Write);
            await content.CopyToAsync(hashing, cancellationToken);
            hashing.FlushFinalBlock();
            sizeBytes = fs.Position;
            hash = sha.Hash ?? Array.Empty<byte>();
        }

        return new AttachmentSavedInfo(relative, sizeBytes, hash);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveAbsolutePath(relativePath);
        Stream s = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return Task.FromResult(s);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveAbsolutePath(relativePath);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }
        return Task.CompletedTask;
    }

    public bool Exists(string relativePath)
    {
        try
        {
            return File.Exists(ResolveAbsolutePath(relativePath));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string ResolveRoot(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.GetFullPath(rootDirectory);
    }

    private string ResolveAbsolutePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(_root, normalized));

        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Resolved path '{combined}' escapes the attachment root '{_root}'.", nameof(relativePath));
        }

        return combined;
    }

    private static string NormalizeExtension(string fileExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);
        var ext = fileExtension.Trim();
        if (!ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        // The extension is caller-supplied (may be derived from a user
        // upload). Reject any character that could change the path
        // shape: separators, traversal segments, drive-spec colons,
        // wildcards, NULs, and Windows path-invalid characters.
        if (ext.Contains('/', StringComparison.Ordinal)
            || ext.Contains('\\', StringComparison.Ordinal)
            || ext.Contains("..", StringComparison.Ordinal)
            || ext.Contains(':', StringComparison.Ordinal)
            || ext.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                $"File extension '{fileExtension}' contains characters that would alter the storage path.",
                nameof(fileExtension));
        }

        return ext;
    }
}
