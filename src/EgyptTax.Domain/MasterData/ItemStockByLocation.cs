namespace EgyptTax.Domain.MasterData;

/// <summary>
/// L4 (v3 roadmap) — per-(item, location) stock count. Sibling to
/// the legacy <see cref="Item.QuantityOnHand"/> (which v1 keeps as
/// the canonical total). Rows are created lazily by the
/// stock-receipt / transfer handlers when a non-zero quantity is
/// recorded for an item at a location; absent row = zero stock.
///
/// Unique composite index on (item_id, location_id) at the EF
/// config layer.
/// </summary>
public sealed class ItemStockByLocation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public Guid LocationId { get; init; }
    public decimal Quantity { get; private set; }

    private ItemStockByLocation() { }

    public ItemStockByLocation(Guid itemId, Guid locationId, decimal initialQuantity = 0m)
    {
        if (itemId == Guid.Empty) throw new ArgumentException("ItemId required.", nameof(itemId));
        if (locationId == Guid.Empty) throw new ArgumentException("LocationId required.", nameof(locationId));
        ArgumentOutOfRangeException.ThrowIfNegative(initialQuantity);
        ItemId = itemId;
        LocationId = locationId;
        Quantity = initialQuantity;
    }

    public void IncreaseStock(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        Quantity += qty;
    }

    public void DecreaseStock(decimal qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        if (Quantity - qty < 0m)
            throw new InvalidOperationException(
                $"Insufficient stock at this location. Have {Quantity}, need {qty}.");
        Quantity -= qty;
    }
}
