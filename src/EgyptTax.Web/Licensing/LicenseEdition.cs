namespace EgyptTax.Web.Licensing;

/// <summary>
/// Gux.13 — the four product editions DaftarX ships in. Encoded
/// into the signed license payload (string form) and read by
/// <see cref="LicenseGate"/> at every gated feature site.
///
/// See <c>specs/008-egypt-tax-accounting/pricing.md</c> for the
/// canonical price + feature mapping. See
/// <c>specs/008-egypt-tax-accounting/gux-13-admin-panel.md</c> §7.2
/// for the full feature matrix.
/// </summary>
public enum LicenseEdition
{
    /// <summary>Unknown / pre-Gux.13 token — treated as Solo
    /// permissions for safety. Old tokens with edition="Standard"
    /// land here.</summary>
    Unknown = 0,

    /// <summary>Single user, single company, core compliance only.
    /// Targets newly-mandated micro-businesses (Decree 281/2025).
    /// 3,500 EGP/year.</summary>
    Solo,

    /// <summary>3 users, 1 company, + bank import + bulk upload +
    /// OCR + WhatsApp + audit log. 8,000 EGP/year.</summary>
    Smb,

    /// <summary>Unlimited users, 3 companies, + income-tax return +
    /// Compliance Health + AI assistant + cloud backup.
    /// 17,500 EGP/year.</summary>
    Enterprise,

    /// <summary>Unlimited users, unlimited companies, + Firm Portal
    /// + commission ledger + bulk-cross-company. 30,000 EGP/year.
    /// Buyer is the accounting firm itself.</summary>
    Firm,

    /// <summary>14-day evaluation. Unlocks all Enterprise features
    /// (NOT Firm Portal). Special-cased by the gate so trial users
    /// get the full Enterprise experience before they decide which
    /// edition to buy.</summary>
    Trial,
}

public static class LicenseEditionExtensions
{
    /// <summary>Parse the wire-format edition string (case-insensitive).
    /// Old "Standard" tokens map to Solo for safety. Unknown strings
    /// also land in Solo.</summary>
    public static LicenseEdition ParseOrSolo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return LicenseEdition.Solo;
        return value.Trim().ToUpperInvariant() switch
        {
            "SOLO"        => LicenseEdition.Solo,
            "SMB"         => LicenseEdition.Smb,
            "ENTERPRISE"  => LicenseEdition.Enterprise,
            "FIRM"        => LicenseEdition.Firm,
            "TRIAL"       => LicenseEdition.Trial,
            // Pre-Gux.13 tokens used "Standard" and "Pro". Map both
            // to Solo conservatively — operator must re-issue to get
            // the new ladder. Refusing entirely would brick installs.
            "STANDARD"    => LicenseEdition.Solo,
            "PRO"         => LicenseEdition.Solo,
            "BASIC"       => LicenseEdition.Solo,
            _             => LicenseEdition.Solo,
        };
    }

    /// <summary>Wire-format string used in the signed payload.
    /// Round-trips through <see cref="ParseOrSolo"/>.</summary>
    public static string ToWireString(this LicenseEdition edition) => edition switch
    {
        LicenseEdition.Solo       => "Solo",
        LicenseEdition.Smb        => "SMB",
        LicenseEdition.Enterprise => "Enterprise",
        LicenseEdition.Firm       => "Firm",
        LicenseEdition.Trial      => "Trial",
        _                         => "Solo",
    };

    /// <summary>Arabic label for the License tab (Tab 7) and upgrade
    /// prompts.</summary>
    public static string ArabicLabel(this LicenseEdition edition) => edition switch
    {
        LicenseEdition.Solo       => "فردي",
        LicenseEdition.Smb        => "أعمال صغيرة",
        LicenseEdition.Enterprise => "مؤسسات",
        LicenseEdition.Firm       => "مكاتب محاسبة",
        LicenseEdition.Trial      => "تجريبي",
        _                         => "غير معروف",
    };

    /// <summary>Default user cap when the license payload doesn't
    /// specify MaxUsers (pre-Gux.13 token).</summary>
    public static int DefaultMaxUsers(this LicenseEdition edition) => edition switch
    {
        LicenseEdition.Solo       => 1,
        LicenseEdition.Smb        => 3,
        LicenseEdition.Enterprise => int.MaxValue,
        LicenseEdition.Firm       => int.MaxValue,
        LicenseEdition.Trial      => int.MaxValue,
        _                         => 1,
    };

    /// <summary>Default company cap when the license payload doesn't
    /// specify MaxCompanies.</summary>
    public static int DefaultMaxCompanies(this LicenseEdition edition) => edition switch
    {
        LicenseEdition.Solo       => 1,
        LicenseEdition.Smb        => 1,
        LicenseEdition.Enterprise => 3,
        LicenseEdition.Firm       => int.MaxValue,
        LicenseEdition.Trial      => 3,
        _                         => 1,
    };
}
