namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v4 A.3 — physical stock-count adjustment. The operator counts a
/// physical location, types each item's actual count, and the
/// difference between actual and system count produces one
/// <see cref="StockAdjustment"/> row per item with a non-zero delta.
///
/// Each adjustment also fans out two side-effects:
///   * The matching <see cref="ItemStockByLocation"/> row is moved
///     to the actual count (created if missing).
///   * A <see cref="StockMovement"/> with <see cref="StockMovementKind.Adjustment"/>
///     records the delta against the item's total
///     <see cref="Item.QuantityOnHand"/> so the existing per-item
///     ledger surfaces the count.
///
/// This entity is the audit row — answers "who counted, when,
/// what location, what item, system showed X, actual was Y, why."
/// One row per (count session, item) — no multi-day count session
/// (Odoo-style mid-count freeze) at the MVP.
/// </summary>
public sealed class StockAdjustment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public Guid LocationId { get; init; }
    public DateTime CountedAtUtc { get; init; }
    public Guid CountedByUserId { get; init; }
    public decimal SystemCount { get; init; }
    public decimal ActualCount { get; init; }
    public decimal Delta { get; init; }
    public StockAdjustmentReason Reason { get; init; }
    public string? Note { get; init; }

    private StockAdjustment() { }

    public StockAdjustment(
        Guid itemId,
        Guid locationId,
        DateTime countedAtUtc,
        Guid countedByUserId,
        decimal systemCount,
        decimal actualCount,
        StockAdjustmentReason reason,
        string? note = null)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("ItemId required.", nameof(itemId));
        }
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("LocationId required.", nameof(locationId));
        }
        if (countedByUserId == Guid.Empty)
        {
            throw new ArgumentException("CountedByUserId required.", nameof(countedByUserId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(systemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(actualCount);
        if (actualCount == systemCount)
        {
            throw new ArgumentException(
                "Actual count equals system count — no adjustment needed; caller should skip this row.");
        }

        ItemId = itemId;
        LocationId = locationId;
        CountedAtUtc = countedAtUtc;
        CountedByUserId = countedByUserId;
        SystemCount = systemCount;
        ActualCount = actualCount;
        Delta = actualCount - systemCount;
        Reason = reason;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>v4 A.3 — operator's reason for a physical count delta.
/// Drives the audit story; doesn't affect the journal posts (for
/// v1 the auto-emit posts a single COGS-style adjustment regardless
/// — accounting impact is folded into the existing
/// <see cref="StockMovementKind.Adjustment"/> path).</summary>
public enum StockAdjustmentReason
{
    /// <summary>Theft / unaccounted disappearance.</summary>
    Shrinkage,
    /// <summary>Item physically broken / spoiled.</summary>
    Damage,
    /// <summary>System was wrong — recount confirms the actual figure.</summary>
    Recount,
    /// <summary>Other; operator should populate Note.</summary>
    Other,
}
