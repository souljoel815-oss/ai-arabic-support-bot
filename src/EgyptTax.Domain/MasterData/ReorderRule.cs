namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v3 §11 #6 (reorder rules) — declarative rule that says
/// "when item X drops to MinQuantity or below, alert me to
/// reorder up to TargetQuantity from this preferred supplier".
///
/// v1 ships ALERT-ONLY: a daily Hangfire job marks items
/// breaching the rule + surfaces them in /reorder-suggestions.
/// Auto-PO generation lands when a real distribution customer
/// asks (v4 trigger per §12).
/// </summary>
public sealed class ReorderRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public decimal MinQuantity { get; private set; }
    public decimal TargetQuantity { get; private set; }
    public Guid? PreferredSupplierId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ReorderRule() { }

    public ReorderRule(Guid itemId, decimal minQty, decimal targetQty, Guid? preferredSupplierId)
    {
        if (itemId == Guid.Empty) throw new ArgumentException("ItemId required.", nameof(itemId));
        ArgumentOutOfRangeException.ThrowIfNegative(minQty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetQty);
        if (targetQty <= minQty)
            throw new ArgumentException("Target quantity must exceed minimum.", nameof(targetQty));
        ItemId = itemId;
        MinQuantity = minQty;
        TargetQuantity = targetQty;
        PreferredSupplierId = preferredSupplierId;
    }

    public void Update(decimal minQty, decimal targetQty, Guid? preferredSupplierId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minQty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetQty);
        if (targetQty <= minQty)
            throw new ArgumentException("Target quantity must exceed minimum.");
        MinQuantity = minQty;
        TargetQuantity = targetQty;
        PreferredSupplierId = preferredSupplierId;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    /// <summary>Recommended order quantity = target − current.
    /// Clamped at 0 if current already exceeds target.</summary>
    public decimal RecommendedOrderQty(decimal currentQty) =>
        Math.Max(0m, TargetQuantity - currentQty);
}
