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

    /// <summary>
    /// Phase K — commission percentage earned on the user's posted
    /// sales. Null = no commission (default for admins/accountants).
    /// Stored as percent (0–100); the commission report multiplies
    /// the rep's net sales by this rate / 100.
    /// </summary>
    public decimal? CommissionRatePercent { get; private set; }

    /// <summary>v5 B.1 — Sales Team membership. Nullable so admins
    /// / accountants stay unassigned. Drives the per-team revenue
    /// rollup on <c>/reports/sales-by-rep</c> when the operator
    /// toggles "By team."</summary>
    public Guid? SalesTeamId { get; private set; }

    /// <summary>v5 D.2.1 — default hourly billing rate (EGP) used
    /// to seed new TimesheetEntry rows for this user. Nullable so
    /// non-billable employees stay unset; the entry form blocks
    /// billable=true when the rate is null. Operator can override
    /// per-entry on the timesheet grid.</summary>
    public decimal? HourlyRateEgp { get; private set; }

    /// <summary>v5 — free-text "remember this about me" notes the user
    /// supplies to the AI chat assistant. Loaded into every NL query's
    /// system prompt so the assistant has persistent context across
    /// sessions ("I'm a sales rep at Company X. I usually ask about
    /// commissions. Show by rep, not by team.").</summary>
    public string? AiMemoryNotes { get; private set; }

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
        bool passwordMustChange = true
    )
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
            throw new ArgumentException(
                "Encrypted MFA secret must be non-empty.",
                nameof(encryptedSecret)
            );
        }
        MfaSecretEncrypted = encryptedSecret;
        MfaEnrolledAtUtc = DateTime.UtcNow;
    }

    public bool RequiresMfa() => Roles.Any(r => r.RequiresMfa);

    /// <summary>v5 B.1 — assign user to a sales team (or detach
    /// when null). The application layer enforces that the team
    /// exists + is Active before calling this.</summary>
    public void AssignToSalesTeam(Guid? teamId)
    {
        if (teamId == Guid.Empty) teamId = null;
        SalesTeamId = teamId;
    }

    public void SetCommissionRate(decimal? ratePercent)
    {
        if (ratePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(ratePercent),
                "Commission rate must be between 0 and 100 percent.");
        CommissionRatePercent = ratePercent;
    }

    /// <summary>v5 D.2.1 — set the user's default hourly billing
    /// rate. Pass null to clear (non-billable user).</summary>
    public void SetHourlyRate(decimal? ratePercent)
    {
        if (ratePercent is < 0)
            throw new ArgumentOutOfRangeException(nameof(ratePercent),
                "Hourly rate cannot be negative.");
        HourlyRateEgp = ratePercent;
    }

    /// <summary>v5 — set the AI chat persistent-memory notes. Empty
    /// string / whitespace is normalized to null (no memory loaded).</summary>
    public void SetAiMemoryNotes(string? notes)
    {
        AiMemoryNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void DisableMfa()
    {
        // FR-002 invariant: MFA cannot be disabled while the user holds a
        // role that requires it. Caller must remove the role first; this
        // method enforces the invariant defensively.
        if (RequiresMfa())
        {
            throw new InvalidOperationException(
                "Cannot disable MFA: user holds at least one role that requires it. Remove the role first."
            );
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
