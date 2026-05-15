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
    /// Phase D — current quantity on hand. Decremented when a sales
    /// invoice line is posted; incremented on credit-note return,
    /// stock receipt, or manual upward adjustment. Decimal so the
    /// system supports both whole-unit goods (laptops) and divisible
    /// goods (kg of rice). Defaults to zero — operators set opening
    /// stock via the Receive Stock screen.
    /// </summary>
    public decimal QuantityOnHand { get; private set; }

    /// <summary>
    /// Phase D — when QuantityOnHand drops below this number, the
    /// item shows up on the dashboard's low-stock badge. Null = no
    /// alert (operator hasn't set a reorder point yet).
    /// </summary>
    public decimal? LowStockThreshold { get; private set; }

    /// <summary>v5 B.2 — operator-set list price in EGP. Null = no
    /// list price configured (operator types the price on every
    /// invoice line). When set, becomes the price the
    /// <c>PricelistResolver</c> applies discount-% rules against;
    /// fixed-price rules ignore this field.</summary>
    public decimal? DefaultUnitPriceEgp { get; private set; }

    /// <summary>v3 §11 #5 (lot tracking) — opt-in flag. When true,
    /// stock receipts must specify a lot code + optional expiry
    /// date; per-lot stock is tracked in <see cref="ItemLot"/>
    /// rows alongside the item-total <see cref="QuantityOnHand"/>.
    /// Default false; flipping to true does not retroactively split
    /// existing stock — operator backfills via the lots page if
    /// they want historical batches recorded.</summary>
    public bool TracksLots { get; private set; }

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

    /// <summary>v5 B.2 — set or clear the list price. Allowed in
    /// any state (operator may add the price retroactively).</summary>
    public void SetDefaultUnitPrice(decimal? priceEgp)
    {
        if (priceEgp is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceEgp),
                "Default unit price cannot be negative.");
        }
        DefaultUnitPriceEgp = priceEgp;
    }

    /// <summary>
    /// Phase D — receive stock or post an upward adjustment. The
    /// caller (StockMovementHandler) is responsible for writing the
    /// matching audit entry; this method just bumps the counter.
    /// </summary>
    public void IncreaseStock(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity),
                "Stock receipt / adjustment quantity must be positive.");
        QuantityOnHand += quantity;
    }

    /// <summary>
    /// Phase D — sell stock (called from sales-invoice post) or post
    /// a downward adjustment. Refuses to go below zero so the post
    /// fails BEFORE the invoice is committed; caller surfaces the
    /// throw to the operator as "insufficient stock".
    /// </summary>
    public void DecreaseStock(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity),
                "Stock decrement quantity must be positive.");
        if (QuantityOnHand < quantity)
            throw new InvalidOperationException(
                $"Insufficient stock for item {Code}: have {QuantityOnHand}, need {quantity}.");
        QuantityOnHand -= quantity;
    }

    public void SetLowStockThreshold(decimal? threshold)
    {
        if (threshold is < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold),
                "Low-stock threshold cannot be negative.");
        LowStockThreshold = threshold;
    }

    public void UpdateDefaultVatCategory(Guid vatCategoryId) =>
        DefaultVatCategoryId = vatCategoryId;

    public void SetEtaItemCode(string? etaItemCode) =>
        EtaItemCode = string.IsNullOrWhiteSpace(etaItemCode) ? null : etaItemCode;

    /// <summary>v3 §11 #5 — opt the item into per-lot tracking.
    /// Operator can flip back to false if they decide it's not
    /// worth the extra data entry; existing ItemLot rows stay
    /// (they're informational once tracking is off).</summary>
    public void SetTracksLots(bool tracks) => TracksLots = tracks;

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
