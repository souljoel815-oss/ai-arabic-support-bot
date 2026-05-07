namespace EgyptTax.Domain.Identity;

/// <summary>
/// FR-038 — single-use, time-bounded token an administrator issues to a
/// user so the user can set a new password without involving the
/// administrator further. Only the SHA-256 hash of the plaintext token
/// is stored; the plaintext is shown once to the issuing admin and then
/// out-of-band-delivered to the user. Default lifetime is 1 hour;
/// once <see cref="RedeemedAtUtc"/> is set, the token is consumed.
/// </summary>
public sealed class PasswordResetToken
{
    public const int DefaultLifetimeHours = 1;

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }

    /// <summary>
    /// SHA-256 hash of the plaintext token (32 bytes). Storing only the
    /// hash means a DB compromise does not let an attacker redeem
    /// outstanding tokens.
    /// </summary>
    public byte[] TokenHash { get; init; } = default!;

    public DateTime IssuedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? RedeemedAtUtc { get; private set; }

    public Guid IssuedByAdminUserId { get; init; }

    private PasswordResetToken() { }

    public PasswordResetToken(
        Guid userId,
        byte[] tokenHash,
        Guid issuedByAdminUserId,
        DateTime nowUtc,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        if (tokenHash.Length != 32)
        {
            throw new ArgumentException("TokenHash must be exactly 32 bytes (SHA-256).", nameof(tokenHash));
        }

        UserId = userId;
        TokenHash = tokenHash;
        IssuedByAdminUserId = issuedByAdminUserId;
        IssuedAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc.Add(lifetime ?? TimeSpan.FromHours(DefaultLifetimeHours));
    }

    public bool IsActive(DateTime nowUtc) =>
        RedeemedAtUtc is null && nowUtc < ExpiresAtUtc;

    public void Redeem(DateTime nowUtc)
    {
        if (RedeemedAtUtc is not null)
        {
            throw new InvalidOperationException("Password reset token has already been redeemed.");
        }
        if (nowUtc >= ExpiresAtUtc)
        {
            throw new InvalidOperationException("Password reset token has expired.");
        }
        RedeemedAtUtc = nowUtc;
    }
}
