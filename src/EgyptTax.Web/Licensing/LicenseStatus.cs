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
    private static volatile int _failureReason = (int)LicenseFailureReason.None;

    public static LicenseState State => (LicenseState)_state;
    public static string Hwid => _hwid;
    public static string CustomerName => _customer;
    public static string SalesPhone => _salesPhone;
    public static string SalesEmail => _salesEmail;
    public static DateTime ExpiresAtUtc => _expiresAtUtc;
    public static LicenseFailureReason FailureReason => (LicenseFailureReason)_failureReason;
    public static bool IsLicensed => State == LicenseState.Active;

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
}
