namespace EgyptTax.Web.Licensing;

/// <summary>
/// Process-wide singleton holding the current license state.
/// Written once by the boot-time gate, read by middleware on every
/// request + scattered check sites. Atomic via volatile read/write
/// of an int (the underlying enum).
/// </summary>
public static class LicenseStatus
{
    private static volatile int _state = (int)LicenseState.NotChecked;
    private static volatile string _hwid = "";
    private static volatile string _customer = "";
    private static volatile string _salesPhone = "+20 100 000 0000";
    private static volatile string _salesEmail = "sales@daftarx.local";
    private static DateTime _expiresAtUtc = DateTime.MinValue;
    private static DateTime _trialExpiresAtUtc = DateTime.MinValue;
    private static volatile int _failureReason = (int)LicenseFailureReason.None;

    public static LicenseState State => (LicenseState)_state;
    public static string Hwid => _hwid;
    public static string CustomerName => _customer;
    public static string SalesPhone => _salesPhone;
    public static string SalesEmail => _salesEmail;
    public static DateTime ExpiresAtUtc => _expiresAtUtc;
    /// <summary>P0 — when <see cref="State"/> is
    /// <see cref="LicenseState.Trial"/>, the wall-clock at which the
    /// trial ends and the install falls back to the unlicensed
    /// banner. <see cref="DateTime.MinValue"/> when not in a trial.</summary>
    public static DateTime TrialExpiresAtUtc => _trialExpiresAtUtc;
    public static LicenseFailureReason FailureReason => (LicenseFailureReason)_failureReason;
    /// <summary>True for paid + trial installs. Read by every
    /// LicenseSentry call site + the banner middleware — both
    /// states are equally permitted to run the app.</summary>
    public static bool IsLicensed => State == LicenseState.Active || State == LicenseState.Trial;

    public static void RecordValid(LicensePayload p, string hwid)
    {
        _hwid = hwid;
        _customer = p.Customer ?? "";
        _salesPhone = string.IsNullOrWhiteSpace(p.SalesPhone) ? _salesPhone : p.SalesPhone;
        _salesEmail = string.IsNullOrWhiteSpace(p.SalesEmail) ? _salesEmail : p.SalesEmail;
        _expiresAtUtc = p.ExpiresAtUtc;
        _failureReason = (int)LicenseFailureReason.None;
        _state = (int)LicenseState.Active;
    }

    /// <summary>
    /// P0 — record an active trial. Called by <see cref="LicenseGate"/>
    /// on first run when no <c>license.token</c> is present, OR on
    /// subsequent boots when the persisted trial marker is still in
    /// its window. <paramref name="trialExpiresAtUtc"/> is the
    /// wall-clock at which the trial flips back to NotActivated.
    /// </summary>
    public static void RecordTrial(string hwid, DateTime trialExpiresAtUtc)
    {
        _hwid = hwid;
        _customer = "Trial";
        _expiresAtUtc = trialExpiresAtUtc;
        _trialExpiresAtUtc = trialExpiresAtUtc;
        _failureReason = (int)LicenseFailureReason.None;
        _state = (int)LicenseState.Trial;
    }

    public static void RecordFailure(LicenseFailureReason r, string hwid, LicensePayload? p = null)
    {
        _hwid = hwid;
        _failureReason = (int)r;
        if (p is not null)
        {
            _customer = p.Customer ?? "";
            _salesPhone = string.IsNullOrWhiteSpace(p.SalesPhone) ? _salesPhone : p.SalesPhone;
            _salesEmail = string.IsNullOrWhiteSpace(p.SalesEmail) ? _salesEmail : p.SalesEmail;
            _expiresAtUtc = p.ExpiresAtUtc;
        }
        _state = r switch
        {
            LicenseFailureReason.Expired             => (int)LicenseState.Expired,
            LicenseFailureReason.EnvelopeMissingOrEmpty => (int)LicenseState.NotActivated,
            _                                        => (int)LicenseState.Tampered,
        };
    }
}

public enum LicenseState
{
    NotChecked,
    Active,
    NotActivated,
    Expired,
    Tampered,
    /// <summary>
    /// P0 — install is running on a 14-day evaluation trial. Full
    /// product functionality is allowed; a countdown banner reminds
    /// the operator how many days remain.
    /// </summary>
    Trial,
}
