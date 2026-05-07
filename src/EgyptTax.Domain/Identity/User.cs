using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Identity;

/// <summary>
/// FR-001 / FR-002 / FR-038 / FR-039 — application user. Holds the
/// minimum identity surface the MVP needs: hashed password, optional
/// TOTP secret (encrypted at rest via the Infrastructure value-converter
/// that wraps DPAPI on Windows; lands in T047), per-user lockout
/// counters, last-login state, and the role memberships.
/// </summary>
public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Email { get; private set; } = default!;
    public ArabicEnglishText DisplayName { get; private set; }
    public string PasswordHash { get; private set; } = default!;
    public bool PasswordMustChange { get; private set; }
    public DateTime? PasswordChangedAtUtc { get; private set; }

    public byte[]? MfaSecretEncrypted { get; private set; }
    public DateTime? MfaEnrolledAtUtc { get; private set; }

    public Language PreferredLanguage { get; private set; } = Language.Ar;
    public UserStatus Status { get; private set; } = UserStatus.Active;

    public DateTime? LastLoginAtUtc { get; private set; }
    public bool LastLoginSucceeded { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutUntilUtc { get; private set; }

    public ICollection<Role> Roles { get; init; } = new List<Role>();

    private User() { }

    public User(
        string email,
        ArabicEnglishText displayName,
        string passwordHash,
        Language preferredLanguage = Language.Ar,
        bool passwordMustChange = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

#pragma warning disable CA1308 // Email canonical form is lowercase; CA1308's uppercase guidance does not apply here.
        Email = email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        DisplayName = displayName;
        PasswordHash = passwordHash;
        PreferredLanguage = preferredLanguage;
        PasswordMustChange = passwordMustChange;
        Status = UserStatus.Active;
    }

    public void SetPassword(string newHash, bool mustChange = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);
        PasswordHash = newHash;
        PasswordChangedAtUtc = DateTime.UtcNow;
        PasswordMustChange = mustChange;
        FailedLoginCount = 0;
        LockoutUntilUtc = null;
    }

    public void EnrollMfa(byte[] encryptedSecret)
    {
        ArgumentNullException.ThrowIfNull(encryptedSecret);
        if (encryptedSecret.Length == 0)
        {
            throw new ArgumentException("Encrypted MFA secret must be non-empty.", nameof(encryptedSecret));
        }
        MfaSecretEncrypted = encryptedSecret;
        MfaEnrolledAtUtc = DateTime.UtcNow;
    }

    public bool RequiresMfa() => Roles.Any(r => r.RequiresMfa);

    public void DisableMfa()
    {
        // FR-002 invariant: MFA cannot be disabled while the user holds a
        // role that requires it. Caller must remove the role first; this
        // method enforces the invariant defensively.
        if (RequiresMfa())
        {
            throw new InvalidOperationException(
                "Cannot disable MFA: user holds at least one role that requires it. Remove the role first.");
        }
        MfaSecretEncrypted = null;
        MfaEnrolledAtUtc = null;
    }

    public void RecordLogin(bool succeeded, DateTime nowUtc)
    {
        LastLoginAtUtc = nowUtc;
        LastLoginSucceeded = succeeded;
        if (succeeded)
        {
            FailedLoginCount = 0;
            LockoutUntilUtc = null;
        }
        else
        {
            FailedLoginCount++;
        }
    }

    public void Lockout(DateTime untilUtc)
    {
        LockoutUntilUtc = untilUtc;
    }

    public bool IsCurrentlyLockedOut(DateTime nowUtc) =>
        LockoutUntilUtc is { } until && until > nowUtc;

    public void Disable() => Status = UserStatus.Disabled;
    public void Reactivate() => Status = UserStatus.Active;
}
