namespace EgyptTax.Domain.MasterData;

/// <summary>
/// Phase D — append-only log of every change to <see cref="Item.QuantityOnHand"/>.
/// Lets the operator reconcile the current stock level with its
/// history (when did the count drop from 50 to 40?). Each row is
/// signed: positive Quantity = increase (receipt / credit-note return
/// / upward adjustment); negative = decrease (sale / downward
/// adjustment).
/// </summary>
public sealed class StockMovement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public decimal Quantity { get; init; }
    public decimal QuantityOnHandAfter { get; init; }
    public StockMovementKind Kind { get; init; }

    /// <summary>
    /// When the movement was triggered by a document (sale, credit
    /// note), this points at the source so the ledger can link back.
    /// Null for pure manual adjustments and opening receipts.
    /// </summary>
    public Guid? SourceDocumentId { get; init; }
    public string? Note { get; init; }
    public Guid? CreatedByUserId { get; init; }

    private StockMovement() { }

    public StockMovement(
        Guid itemId,
        DateTime occurredAtUtc,
        decimal quantity,
        decimal quantityOnHandAfter,
        StockMovementKind kind,
        Guid? sourceDocumentId = null,
        string? note = null,
        Guid? createdByUserId = null)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("ItemId required.", nameof(itemId));
        if (quantity == 0)
            throw new ArgumentOutOfRangeException(nameof(quantity),
                "Stock movement quantity cannot be zero.");
        ItemId = itemId;
        OccurredAtUtc = occurredAtUtc;
        Quantity = quantity;
        QuantityOnHandAfter = quantityOnHandAfter;
        Kind = kind;
        SourceDocumentId = sourceDocumentId;
        Note = note;
        CreatedByUserId = createdByUserId;
    }
}

public enum StockMovementKind
{
    /// <summary>Operator entered an opening balance for a fresh install.</summary>
    OpeningBalance,
    /// <summary>Stock received from a supplier (could be linked to a purchase invoice later).</summary>
    Receipt,
    /// <summary>Item sold via a sales invoice. Quantity is negative.</summary>
    Sale,
    /// <summary>Returned via a credit note. Quantity is positive.</summary>
    Return,
    /// <summary>Manual count adjustment (shrinkage, breakage, recount). Quantity may be either sign.</summary>
    Adjustment,
}
