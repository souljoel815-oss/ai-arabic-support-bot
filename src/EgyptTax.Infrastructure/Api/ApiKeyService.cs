using System.Security.Cryptography;
using System.Text;
using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Api;

/// <summary>
/// N.3 (v3 §11) — mint + verify API keys for the public REST
/// surface. Tokens are 32 random bytes (256 bits) base64url-
/// encoded with a "dx_" prefix for grep-ability in operator
/// logs. We store SHA-256(token) so the plaintext can't be
/// recovered if the database is dumped.
/// </summary>
public sealed class ApiKeyService
{
    private const string TokenPrefix = "dx_";
    private const int TokenEntropyBytes = 32;
    private const int DisplayPrefixLength = 8;

    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public ApiKeyService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Mints a fresh API key. Returns the plaintext token ONCE —
    /// caller (the management page) must show it to the operator
    /// immediately, then discard. After this method returns the
    /// plaintext is unrecoverable; only the hash is stored.
    /// </summary>
    public async Task<MintedApiKey> MintAsync(
        string name,
        Guid? createdByUserId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var bytes = RandomNumberGenerator.GetBytes(TokenEntropyBytes);
        var random = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').Replace("=", "", StringComparison.Ordinal);
        var token = TokenPrefix + random;
        var displayPrefix = TokenPrefix + random[..DisplayPrefixLength];
        var hash = HashToken(token);

        var entity = new ApiKey(
            name: name,
            displayPrefix: displayPrefix,
            keyHash: hash,
            createdAtUtc: _clock.UtcNow,
            createdByUserId: createdByUserId);
        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new MintedApiKey(entity.Id, token, displayPrefix);
    }

    /// <summary>
    /// Look up the key from a presented token. Returns null when
    /// not found or revoked. On success records the last-used
    /// timestamp (best-effort — failures swallowed so a logging
    /// blip doesn't break the API request).
    /// </summary>
    public async Task<ApiKey?> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal)) return null;

        var hash = HashToken(token);
        var key = await _db.Set<ApiKey>()
            .FirstOrDefaultAsync(k => k.KeyHash == hash, ct);
        if (key is null || key.Revoked) return null;

        try
        {
            key.RecordUse(_clock.UtcNow);
            await _db.SaveChangesAsync(ct);
        }
        catch { /* best-effort */ }
        return key;
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    public sealed record MintedApiKey(Guid Id, string Token, string DisplayPrefix);
}
