namespace EgyptTax.Domain.Settings;

/// <summary>
/// N.3 (v3 §11) — API key for the public REST surface. Stored as
/// SHA-256 of the full token; only the operator-visible prefix
/// (first 8 chars after "dx_") is kept in cleartext for display
/// in the management UI ("dx_AbCdEfGh…").
///
/// Token format: <c>dx_&lt;43 base64url chars&gt;</c> = ~44 chars
/// total, ~256 bits of entropy. Generated once at mint time and
/// shown to the operator EXACTLY ONCE — they're responsible for
/// copying it to their integration's config. We can't recover
/// the plaintext after mint because we only store the hash.
///
/// Revocation: <see cref="Revoke"/> sets <see cref="Revoked"/>;
/// the auth middleware refuses revoked keys. Soft-delete only
/// (kept for audit so operators can see "this key was used 47
/// times before I revoked it on 2026-04-12").
/// </summary>
public sealed class ApiKey
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Operator-given label, e.g. "Shopify integration".</summary>
    public string Name { get; private set; } = "";

    /// <summary>First 8 chars of the random part, kept in clear so
    /// the operator can identify the key in the list.</summary>
    public string DisplayPrefix { get; init; } = "";

    /// <summary>SHA-256 hex of the full token (lowercase, 64 chars).</summary>
    public string KeyHash { get; init; } = "";

    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public bool Revoked { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private ApiKey() { }

    public ApiKey(
        string name,
        string displayPrefix,
        string keyHash,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);
        Name = name.Trim();
        DisplayPrefix = displayPrefix;
        KeyHash = keyHash;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void RecordUse(DateTime nowUtc) => LastUsedAtUtc = nowUtc;

    public void Revoke(DateTime nowUtc)
    {
        Revoked = true;
        RevokedAtUtc = nowUtc;
    }
}
