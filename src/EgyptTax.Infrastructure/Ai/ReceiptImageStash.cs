using Microsoft.Extensions.Caching.Memory;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// v4 A.4 — short-lived holding pen for the original receipt image
/// bytes between <c>/scan-receipt</c> (where the operator picks the
/// file + Claude reads it) and <c>/expenses/new</c> (where they
/// confirm + save the draft). The bytes are stashed under a token
/// returned to the caller; the caller embeds the token in the
/// redirect URL. The expense-edit page retrieves them after
/// <c>SaveChangesAsync</c> and attaches them via the existing
/// <c>UploadAttachmentHandler</c> pipeline.
///
/// Why in-memory: the image is 1-5 MiB, lives for &lt; 60 seconds
/// in normal flow (the operator clicks "Create expense" then saves
/// almost immediately), and we don't want a stash row in the DB
/// that we'd then have to GC. <see cref="IMemoryCache"/> with a
/// 15-min absolute expiry covers the slow case + auto-evicts.
/// </summary>
public sealed class ReceiptImageStash
{
    private static readonly TimeSpan StashTtl = TimeSpan.FromMinutes(15);
    private const string KeyPrefix = "receipt-stash:";

    private readonly IMemoryCache _cache;

    public ReceiptImageStash(IMemoryCache cache)
    {
        _cache = cache;
    }

    public sealed record StashedImage(byte[] Bytes, string FileName, string MimeType);

    /// <summary>Stash the bytes and return a URL-safe token.</summary>
    public string Stash(byte[] bytes, string fileName, string mimeType)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        var token = Guid.NewGuid().ToString("N");
        _cache.Set(
            KeyPrefix + token,
            new StashedImage(bytes, fileName, mimeType),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = StashTtl,
                Size = bytes.LongLength,
            });
        return token;
    }

    /// <summary>
    /// Retrieve and remove the bytes for the token. Returns null if
    /// the token has expired, was already consumed, or never existed.
    /// </summary>
    public StashedImage? Take(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = KeyPrefix + token;
        if (_cache.TryGetValue(key, out StashedImage? stashed))
        {
            _cache.Remove(key);
            return stashed;
        }
        return null;
    }
}
