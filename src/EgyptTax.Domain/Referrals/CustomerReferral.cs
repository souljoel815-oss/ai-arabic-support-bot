namespace EgyptTax.Domain.Referrals;

/// <summary>
/// G4.3 — a referral the operator made to someone outside their
/// install. The customer types who they told (a name + a contact),
/// shares their referral code with that person, and tracks the
/// outcome over time.
///
/// Lifecycle:
///   Invited → Installed → Purchased
///
/// Invited = "I told Hany about DaftarX." Nothing has happened yet.
/// Installed = "Hany downloaded and started the trial." The
/// operator usually flips this manually based on word of mouth;
/// the vendor's installer telemetry could fill it automatically
/// in v2 (out of scope today).
/// Purchased = "Hany paid for a license." This is what unlocks the
/// 30-day reward credit — the vendor reconciles via Issue-License
/// when the referred customer activates, and a future
/// self-service portal (G1.5) applies the credit on next renewal.
///
/// Storage note: there's no shared backend across installs, so the
/// ledger here is the OPERATOR'S OWN view of who they've referred.
/// The vendor maintains the canonical reconciliation log elsewhere
/// (the license-issuance pipeline). Operators can see "I told 7
/// people, 3 installed, 1 bought" without waiting for vendor
/// emails.
/// </summary>
public sealed class CustomerReferral
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The person the operator told. Free-text — we don't
    /// require a structured contact since the SMB owner usually
    /// just remembers a name.</summary>
    public string ReferredContactName { get; init; } = "";

    /// <summary>Optional phone/email/handle the operator used. Helps
    /// the vendor reconcile when the referred party eventually
    /// activates (they paste the same contact at checkout).</summary>
    public string? ReferredContactDetail { get; private set; }

    public DateTime InvitedAtUtc { get; init; }

    public CustomerReferralStatus Status { get; private set; }
        = CustomerReferralStatus.Invited;

    public DateTime? InstalledAtUtc { get; private set; }
    public DateTime? PurchasedAtUtc { get; private set; }

    /// <summary>HWID printed on the referred customer's install once
    /// they activate. Captured for vendor-side reconciliation.</summary>
    public string? ReferredCustomerHwid { get; private set; }

    /// <summary>Days of free credit the operator has accrued from
    /// THIS referral (default 30 once Purchased fires). Stored on
    /// the row so retroactive policy changes don't sweep historical
    /// rows.</summary>
    public int RewardDays { get; private set; }

    public string? Note { get; private set; }

    private CustomerReferral() { }

    public CustomerReferral(
        string referredContactName,
        DateTime invitedAtUtc,
        string? referredContactDetail = null,
        string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referredContactName);

        ReferredContactName = referredContactName.Trim();
        ReferredContactDetail = string.IsNullOrWhiteSpace(referredContactDetail)
            ? null
            : referredContactDetail.Trim();
        InvitedAtUtc = invitedAtUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void MarkInstalled(DateTime installedAtUtc)
    {
        if (Status == CustomerReferralStatus.Purchased) return; // already past this
        InstalledAtUtc ??= installedAtUtc;
        if (Status == CustomerReferralStatus.Invited)
            Status = CustomerReferralStatus.Installed;
    }

    public void MarkPurchased(DateTime purchasedAtUtc, string? referredCustomerHwid, int rewardDays = 30)
    {
        if (rewardDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(rewardDays),
                "Reward must be 1-365 days.");

        if (Status == CustomerReferralStatus.Purchased) return; // idempotent

        PurchasedAtUtc = purchasedAtUtc;
        Status = CustomerReferralStatus.Purchased;
        RewardDays = rewardDays;
        if (!string.IsNullOrWhiteSpace(referredCustomerHwid))
            ReferredCustomerHwid = referredCustomerHwid.Trim().ToUpperInvariant();

        // Backfill Installed if we skipped straight to Purchased
        // (operator might not have known until reading the vendor
        // notification).
        InstalledAtUtc ??= purchasedAtUtc;
    }

    public void UpdateContactDetail(string? contactDetail) =>
        ReferredContactDetail = string.IsNullOrWhiteSpace(contactDetail) ? null : contactDetail.Trim();

    public void UpdateNote(string? note) =>
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}

public enum CustomerReferralStatus
{
    /// <summary>Operator told this person; they haven't installed
    /// DaftarX yet (or the operator doesn't know they have).</summary>
    Invited,
    /// <summary>Referred party installed and is using the trial.</summary>
    Installed,
    /// <summary>Referred party bought a license — operator's 30-day
    /// credit is locked in.</summary>
    Purchased,
}
