namespace EgyptTax.Application.Compliance.PenaltyShield;

/// <summary>
/// Egyptian e-invoicing penalty regime constants, baked into the app
/// per Resolution 281 of 2025 + the 2026 enforcement schedule.
///
/// Three escalation tiers when an invoice's submission window expires
/// without an accepted submission:
///   - Tier 1 (Warning): no fine, on the regulator's radar
///   - Tier 2 (5K EGP per invoice, capped 50K/month)
///   - Tier 3 (10K EGP per invoice, no cap, B2B suspension threat)
///
/// The thresholds + amounts here are versioned source of truth; in v2
/// they should move to a signed YAML config that updates monthly per
/// CT.4 in the post-MVP roadmap.
/// </summary>
public static class PenaltyRegime
{
    /// <summary>
    /// Per-invoice fine when the issuer enters Tier 2 (warning has
    /// already been issued and the next late submission triggers a
    /// monetary penalty). Capped at <see cref="Tier2MonthlyCap"/>.
    /// </summary>
    public const decimal Tier2PerInvoiceFineEgp = 5_000m;

    /// <summary>
    /// Monthly cap on Tier 2 fines, regardless of how many invoices
    /// were late in that month. Beyond this cap, escalation jumps
    /// straight to Tier 3.
    /// </summary>
    public const decimal Tier2MonthlyCap = 50_000m;

    /// <summary>
    /// Per-invoice fine in Tier 3. Uncapped — and the regulator
    /// reserves the right to suspend B2B invoicing for the issuer.
    /// </summary>
    public const decimal Tier3PerInvoiceFineEgp = 10_000m;

    /// <summary>
    /// Number of late submissions in the trailing window that bumps an
    /// issuer from Warning into Tier 2.
    /// </summary>
    public const int Tier2LateThreshold = 1;

    /// <summary>
    /// Number of late submissions that bumps from Tier 2 into Tier 3.
    /// Calibrated to the "third strike" pattern in regulator guidance.
    /// </summary>
    public const int Tier3LateThreshold = 10;

    /// <summary>
    /// Trailing window (days) over which late submissions are counted
    /// toward tier escalation. The regulator uses a rolling 12-month
    /// window per Resolution 281/2025.
    /// </summary>
    public const int RollingWindowDays = 365;

    public static decimal ComputeTier2MonthFine(int lateInvoicesThisMonth) =>
        Math.Min(lateInvoicesThisMonth * Tier2PerInvoiceFineEgp, Tier2MonthlyCap);

    public static decimal ComputeTier3MonthFine(int lateInvoicesThisMonth) =>
        lateInvoicesThisMonth * Tier3PerInvoiceFineEgp;
}

/// <summary>
/// The escalation level an issuer currently sits at. Drives the
/// penalty-shield UI tone (green → amber → red) and the projected-fine
/// calculation per <see cref="PenaltyRegime"/>.
/// </summary>
public enum PenaltyTier
{
    /// <summary>Zero late submissions in the rolling window. All clear.</summary>
    Clear = 0,
    /// <summary>1+ late submission but under Tier 2 escalation threshold.</summary>
    Warning = 1,
    /// <summary>EGP 5K per late invoice, monthly cap 50K.</summary>
    Tier2 = 2,
    /// <summary>EGP 10K per late invoice, no cap, B2B suspension threat.</summary>
    Tier3 = 3,
}
