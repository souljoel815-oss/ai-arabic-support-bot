namespace EgyptTax.Domain.FirmPortal;

/// <summary>
/// G1.4 — accountant-referral / commission record. Captures one
/// instance of a registered firm bringing in a paying client, the
/// commission earned (defaults to 20% of first-year licence
/// revenue), and its payout state.
///
/// Lifecycle:
///   Pending → Earned → Paid
///
/// Pending = referral recorded but the customer hasn't paid yet
/// (e.g., still in trial).
/// Earned = customer paid; commission is owed but the vendor
/// hasn't disbursed it yet.
/// Paid = vendor sent the commission via InstaPay / bank transfer
/// and recorded the reference number.
///
/// Why a separate entity (not just "firm_user.referrals_count"):
/// the audit trail needs per-customer rows so the firm can see
/// which clients drove which commission, and the vendor needs a
/// per-row paid-or-not flag for the finance ledger.
///
/// FK: <see cref="AccountantFirmUserId"/> → AccountantFirmUser.UserId.
/// We don't FK to a DaftarX Customer/Company entity because the
/// referred client lives on a SEPARATE on-prem install — there's
/// no shared DB. The referred-client identity is captured by name
/// + HWID + license-token issued (so both sides can reconcile).
/// </summary>
public sealed class AccountantReferral
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>FK → AccountantFirmUser.UserId (the firm partner
    /// who brought the customer).</summary>
    public Guid AccountantFirmUserId { get; init; }

    /// <summary>Customer-facing name as it appears on the licence
    /// (e.g., "Hope Trading Co").</summary>
    public string ReferredCustomerName { get; init; } = "";

    /// <summary>HWID printed on the customer's install — uniquely
    /// identifies the install the licence was issued for. Lets us
    /// dedupe referrals (one HWID can only be referred once).</summary>
    public string ReferredCustomerHwid { get; init; } = "";

    /// <summary>License edition the customer purchased (Basic /
    /// Standard / Pro / Enterprise — free-text since the pricing
    /// tier table isn't a DB entity yet).</summary>
    public string LicenseEdition { get; init; } = "";

    /// <summary>Annual licence price in EGP at the time of the
    /// referral. Snap-shotted so future price changes don't
    /// retroactively alter past commissions.</summary>
    public decimal LicenseAnnualPriceEgp { get; init; }

    /// <summary>Commission rate as a percentage (default 20% of
    /// first-year revenue per the v2 roadmap). Stored per-row so
    /// promotional rates / individual deals can override the
    /// default without changing future rows.</summary>
    public decimal CommissionRatePercent { get; init; }

    /// <summary>Computed at save time:
    /// LicenseAnnualPriceEgp × CommissionRatePercent / 100.
    /// Stored so the ledger is queryable without a JOIN.</summary>
    public decimal CommissionEgp { get; private set; }

    public DateTime ReferredAtUtc { get; init; }

    public AccountantReferralStatus Status { get; private set; }
        = AccountantReferralStatus.Pending;

    public DateTime? EarnedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? PaidReference { get; private set; }

    /// <summary>Free-text note — useful for "trial extended", "client
    /// disputed", "deal renegotiated", etc.</summary>
    public string? Note { get; private set; }

    private AccountantReferral() { }

    public AccountantReferral(
        Guid accountantFirmUserId,
        string referredCustomerName,
        string referredCustomerHwid,
        string licenseEdition,
        decimal licenseAnnualPriceEgp,
        decimal commissionRatePercent,
        DateTime referredAtUtc,
        string? note = null)
    {
        if (accountantFirmUserId == Guid.Empty)
            throw new ArgumentException("AccountantFirmUserId is required.", nameof(accountantFirmUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(referredCustomerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(referredCustomerHwid);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseEdition);
        if (licenseAnnualPriceEgp <= 0m)
            throw new ArgumentOutOfRangeException(nameof(licenseAnnualPriceEgp),
                "License price must be positive.");
        if (commissionRatePercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(commissionRatePercent),
                "Commission rate must be between 0 and 100.");

        AccountantFirmUserId = accountantFirmUserId;
        ReferredCustomerName = referredCustomerName.Trim();
        ReferredCustomerHwid = referredCustomerHwid.Trim().ToUpperInvariant();
        LicenseEdition = licenseEdition.Trim();
        LicenseAnnualPriceEgp = licenseAnnualPriceEgp;
        CommissionRatePercent = commissionRatePercent;
        CommissionEgp = decimal.Round(licenseAnnualPriceEgp * commissionRatePercent / 100m, 2);
        ReferredAtUtc = referredAtUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>
    /// Customer's licence payment cleared — the commission is now
    /// owed to the firm. Idempotent: re-marking earned keeps the
    /// original timestamp so the audit trail is stable.
    /// </summary>
    public void MarkEarned(DateTime earnedAtUtc)
    {
        if (Status == AccountantReferralStatus.Paid)
            throw new InvalidOperationException(
                $"Referral {Id} is already Paid — can't move back to Earned.");
        EarnedAtUtc ??= earnedAtUtc;
        Status = AccountantReferralStatus.Earned;
    }

    /// <summary>
    /// Vendor disbursed the commission — record the payment
    /// reference (InstaPay transaction id, bank transfer reference,
    /// etc.) so finance can reconcile.
    /// </summary>
    public void MarkPaid(DateTime paidAtUtc, string paidReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paidReference);
        if (Status == AccountantReferralStatus.Pending)
            throw new InvalidOperationException(
                $"Referral {Id} is still Pending (customer hasn't paid). Mark earned first.");
        if (Status == AccountantReferralStatus.Paid)
            throw new InvalidOperationException(
                $"Referral {Id} is already Paid (paid at {PaidAtUtc:o}, ref {PaidReference}).");
        PaidAtUtc = paidAtUtc;
        PaidReference = paidReference.Trim();
        Status = AccountantReferralStatus.Paid;
    }

    public void UpdateNote(string? note)
    {
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

public enum AccountantReferralStatus
{
    /// <summary>Referral recorded; customer hasn't paid yet (in
    /// trial or activation pending).</summary>
    Pending,
    /// <summary>Customer paid — commission is owed to the firm.</summary>
    Earned,
    /// <summary>Commission has been disbursed to the firm.</summary>
    Paid,
}
