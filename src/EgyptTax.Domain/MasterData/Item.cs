using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B4 — item master record. The chart-of-account FKs
/// (<c>default_revenue_account_id</c>, <c>default_expense_account_id</c>)
/// referenced in data-model B4 are deferred until US5 introduces the
/// chart of accounts; the MVP slice uses the VAT category alone to
/// drive line tax calculation.
/// </summary>
public sealed class Item
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public Guid DefaultVatCategoryId { get; private set; }
    public ItemStatus Status { get; private set; } = ItemStatus.Active;

    /// <summary>
    /// FR-035 / Differentiator 1 — ETA's GS1-style item code from
    /// the regulator's master commodity list (assigned per item by
    /// the operator). Optional in the MVP because not every demo
    /// item has a real code yet; the
    /// <c>MissingEtaCodeRule</c> flags posted documents whose lines
    /// reference items without a code so the operator knows to
    /// populate it before live filing.
    /// </summary>
    public string? EtaItemCode { get; private set; }

    /// <summary>
    /// P1.6 — kind of code the operator chose to register: GS1 (24-48h
    /// cache lookup) or EGS (15-day approval process at ETA). Stays
    /// null until the operator triggers a code request.
    /// </summary>
    public EtaItemCodeKind? EtaCodeKind { get; private set; }

    /// <summary>
    /// P1.6 — current state of the code-registration flow. Starts at
    /// <see cref="EtaItemCodeStatus.None"/>; the request transitions
    /// it to PendingGs1/PendingEgs; the daily check job promotes to
    /// Active or Failed.
    /// </summary>
    public EtaItemCodeStatus EtaCodeStatus { get; private set; } = EtaItemCodeStatus.None;

    /// <summary>When the operator submitted the code request; drives the countdown.</summary>
    public DateTime? EtaCodeRequestedAtUtc { get; private set; }

    /// <summary>When the registry confirmed the code is Active and citable.</summary>
    public DateTime? EtaCodeActivatedAtUtc { get; private set; }

    /// <summary>Set when EtaCodeStatus is Failed — explains why the registry rejected the code.</summary>
    public string? EtaCodeFailureReason { get; private set; }

    private Item() { }

    public Item(
        string code,
        ArabicEnglishText name,
        Guid defaultVatCategoryId,
        string? etaItemCode = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (defaultVatCategoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Item must reference a default VAT category.",
                nameof(defaultVatCategoryId)
            );
        }
        Code = code;
        Name = name;
        DefaultVatCategoryId = defaultVatCategoryId;
        EtaItemCode = string.IsNullOrWhiteSpace(etaItemCode) ? null : etaItemCode;
    }

    public void Deactivate() => Status = ItemStatus.Inactive;

    public void Reactivate() => Status = ItemStatus.Active;

    public void UpdateDefaultVatCategory(Guid vatCategoryId) =>
        DefaultVatCategoryId = vatCategoryId;

    public void SetEtaItemCode(string? etaItemCode) =>
        EtaItemCode = string.IsNullOrWhiteSpace(etaItemCode) ? null : etaItemCode;

    /// <summary>
    /// P1.6 — record that the operator submitted a code request to
    /// the chosen registry. Idempotent re-request: re-asking with the
    /// same kind while still pending is a no-op (avoids restarting
    /// the countdown when the operator clicks the button twice).
    /// </summary>
    public void RequestEtaCode(EtaItemCodeKind kind, DateTime nowUtc)
    {
        if (EtaCodeStatus == EtaItemCodeStatus.Active)
        {
            throw new InvalidOperationException(
                $"Item {Id} already has an Active code; request a new one is invalid.");
        }
        if (EtaCodeStatus is EtaItemCodeStatus.PendingGs1 or EtaItemCodeStatus.PendingEgs
            && EtaCodeKind == kind)
        {
            return; // idempotent
        }
        EtaCodeKind = kind;
        EtaCodeStatus = kind == EtaItemCodeKind.Gs1
            ? EtaItemCodeStatus.PendingGs1
            : EtaItemCodeStatus.PendingEgs;
        EtaCodeRequestedAtUtc = nowUtc;
        EtaCodeActivatedAtUtc = null;
        EtaCodeFailureReason = null;
    }

    /// <summary>
    /// P1.6 — registry confirmed the code is now Active. Stores the
    /// citable code on <see cref="EtaItemCode"/> so downstream
    /// invoice-line emit picks it up.
    /// </summary>
    public void MarkEtaCodeActive(string code, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        EtaCodeStatus = EtaItemCodeStatus.Active;
        EtaItemCode = code;
        EtaCodeActivatedAtUtc = nowUtc;
        EtaCodeFailureReason = null;
    }

    /// <summary>
    /// P1.6 — registry rejected the code request. The operator can
    /// re-submit (typically after fixing a missing GTIN field) which
    /// resets state via <see cref="RequestEtaCode"/>.
    /// </summary>
    public void MarkEtaCodeFailed(string reason, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EtaCodeStatus = EtaItemCodeStatus.Failed;
        EtaCodeFailureReason = reason;
    }
}

public enum ItemStatus
{
    Active,
    Inactive,
}

/// <summary>P1.6 — registry the operator chose to obtain the ETA item code from.</summary>
public enum EtaItemCodeKind
{
    /// <summary>GS1 Egypt — fast (~24-48h), requires GTIN registration.</summary>
    Gs1,
    /// <summary>EGS — direct via ETA, slow (~15 days), no GS1 dependency.</summary>
    Egs,
}

/// <summary>P1.6 — lifecycle of the item-code registration flow.</summary>
public enum EtaItemCodeStatus
{
    /// <summary>No code requested yet.</summary>
    None,
    /// <summary>GS1 Egypt request submitted; awaiting cache propagation (24-48h).</summary>
    PendingGs1,
    /// <summary>EGS request submitted to ETA; awaiting approval (~15 days).</summary>
    PendingEgs,
    /// <summary>Active and citable on invoice lines.</summary>
    Active,
    /// <summary>Registry rejected the request; operator must fix and re-request.</summary>
    Failed,
}
