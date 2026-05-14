namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v3 §11 #5 (lot tracking) — per-batch stock for items that
/// opted into lot tracking via <see cref="Item.TracksLots"/>.
/// One row per (item × lot_code) with the remaining quantity +
/// optional expiry date.
///
/// Sales/transfers from a lot-tracked item ideally pick the lot
/// FIFO by oldest expiry; v1 ships visibility (lots can be seen
/// + manually adjusted) but the auto-FIFO pick on sales-invoice
/// post lands in a follow-on commit when a real pharmacy customer
/// asks. The dashboard expiring-soon widget surfaces lots within
/// 30 days of expiry so the operator can act before the auto-pick
/// matters.
///
/// Per-unit (serial) tracking is intentionally NOT in this entity
/// — it's a heavier feature for electronics / vehicles and lands
/// when an electronics customer asks (v4 trigger per §12).
/// </summary>
public sealed class ItemLot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    /// <summary>Operator-assigned batch code (e.g. "LOT-2026-A042",
    /// "B7-2025-12-15"). Unique per item.</summary>
    public string LotCode { get; init; } = "";
    /// <summary>Expiry date if applicable (null for non-perishable
    /// items being tracked for traceability without expiry).</summary>
    public DateOnly? ExpiryDate { get; private set; }
    public decimal QuantityOnHand { get; private set; }
    public DateTime ReceivedAtUtc { get; init; }
    /// <summary>Optional supplier reference at receipt time —
    /// supplier name as freeform text for traceability without
    /// requiring a Supplier FK (could be a one-off purchase).</summary>
    public string? SupplierReference { get; init; }

    private ItemLot() { }

    public ItemLot(
        Guid itemId,
        string lotCode,
        decimal initialQuantity,
        DateOnly? expiryDate,
        DateTime receivedAtUtc,
        string? supplierReference)
    {
        if (itemId == Guid.Empty) throw new ArgumentException("ItemId required.", nameof(itemId));
        ArgumentException.ThrowIfNullOrWhiteSpace(lotCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialQuantity);
        ItemId = itemId;
        LotCode = lotCode.Trim();
        QuantityOnHand = initialQuantity;
        ExpiryDate = expiryDate;
        ReceivedAtUtc = receivedAtUtc;
        SupplierReference = supplierReference;
    }

    public void IncreaseStock(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        QuantityOnHand += qty;
    }

    public void DecreaseStock(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        if (QuantityOnHand - qty < 0m)
            throw new InvalidOperationException(
                $"Lot {LotCode}: insufficient stock. Have {QuantityOnHand}, need {qty}.");
        QuantityOnHand -= qty;
    }

    /// <summary>True iff the lot has an expiry AND it's within
    /// the threshold AND there's still stock to worry about.</summary>
    public bool IsExpiringWithin(int days, DateOnly today) =>
        QuantityOnHand > 0m
        && ExpiryDate is { } exp
        && exp.DayNumber - today.DayNumber <= days
        && exp >= today;

    /// <summary>True iff the lot has expired AND there's still
    /// stock that shouldn't be sold.</summary>
    public bool IsExpired(DateOnly today) =>
        QuantityOnHand > 0m
        && ExpiryDate is { } exp
        && exp < today;
}
