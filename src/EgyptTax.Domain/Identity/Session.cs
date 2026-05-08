namespace EgyptTax.Domain.Identity;

/// <summary>
/// FR-039 — server-side session record. The authentication cookie carries
/// only the <see cref="Id"/>; revocation, last-activity tracking, and the
/// FR-039 inactivity (30 min) + absolute (12 h) expiry windows are
/// computed against this row on every request via
/// <c>SessionService.ValidateAsync</c>. The cookie itself is not the
/// source of truth — this row is, so administrator-initiated revocation
/// takes effect on the next protected-resource request.
/// </summary>
public sealed class Session
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public DateTime IssuedAtUtc { get; init; }
    public DateTime LastActivityAtUtc { get; private set; }
    public DateTime AbsoluteExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; private set; }
    public SessionRevocationReason? RevocationReason { get; private set; }

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }

    private Session() { }

    public Session(
        Guid userId,
        DateTime nowUtc,
        TimeSpan absoluteLifetime,
        string? ipAddress,
        string? userAgent
    )
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id must be supplied.", nameof(userId));
        }

        UserId = userId;
        IssuedAtUtc = nowUtc;
        LastActivityAtUtc = nowUtc;
        AbsoluteExpiresAtUtc = nowUtc.Add(absoluteLifetime);
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public void Touch(DateTime nowUtc)
    {
        if (IsRevoked)
        {
            return;
        }
        LastActivityAtUtc = nowUtc;
    }

    public void Revoke(DateTime nowUtc, SessionRevocationReason reason)
    {
        if (IsRevoked)
        {
            return;
        }
        RevokedAtUtc = nowUtc;
        RevocationReason = reason;
    }
}

/// <summary>
/// Why a session is no longer valid. Persisted alongside
/// <c>RevokedAtUtc</c> so an auditor reading the row knows whether the
/// user logged out, hit FR-039 inactivity, hit FR-039 absolute, or was
/// revoked by an administrator.
/// </summary>
public enum SessionRevocationReason
{
    UserLogout,
    InactivityTimeout,
    AbsoluteTimeout,
    AdministratorRevoked,
    PasswordChanged,
    AccountDisabled,
}
