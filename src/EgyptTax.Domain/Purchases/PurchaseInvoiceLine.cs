using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Purchases;

/// <summary>
/// One billable line on a purchase invoice. Per data-model C2 a
/// line carries either an <see cref="ItemId"/> (for inventory /
/// trading goods) OR an <see cref="ExpenseCategoryId"/> (for
/// pure-expense lines that hit the deductible-category surface).
/// Exactly one of the two MUST be non-null — enforced in the
/// constructor.
///
/// <see cref="DeductibleFlag"/> defaults from the
/// DeductibleExpenseCategory's `default_deductible` (the caller
/// resolves the default and passes it here); operator overrides are
/// allowed but the change is recorded in the audit log per FR-015 +
/// FR-027 — that audit hook lives in the application-layer post
/// handler, not in this entity.
/// </summary>
public sealed class PurchaseInvoiceLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PurchaseInvoiceId { get; init; }
    public Guid? ItemId { get; init; }
    public Guid? ExpenseCategoryId { get; init; }
    public decimal Quantity { get; init; }
    public MoneyEgp UnitPrice { get; init; }
    public Guid VatCategoryId { get; init; }
    public decimal VatRatePercent { get; init; }
    public bool DeductibleFlag { get; init; }

    /// <summary>v4 C.2 — optional per-line cost-center tag.
    /// Mirrors the SalesInvoiceLine column; when set, the
    /// cost-centers report attributes this line's cost
    /// (LineSubtotal) to the matching center.</summary>
    public Guid? CostCenterId { get; init; }

    public MoneyEgp LineSubtotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp LineVat { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp LineTotal { get; private set; } = MoneyEgp.Zero;

    private PurchaseInvoiceLine() { }

    public PurchaseInvoiceLine(
        Guid purchaseInvoiceId,
        Guid? itemId,
        Guid? expenseCategoryId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent,
        bool deductibleFlag,
        Guid? costCenterId = null
    )
    {
        // Exactly one of ItemId / ExpenseCategoryId — enforces the
        // C2 data-model invariant. Caller decides which side based
        // on what the supplier billed.
        if ((itemId is null) == (expenseCategoryId is null))
        {
            throw new ArgumentException(
                "Exactly one of ItemId / ExpenseCategoryId must be supplied (XOR)."
            );
        }
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }
        if (unitPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Unit price cannot be negative."
            );
        }
        if (vatRatePercent < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vatRatePercent),
                "VAT rate cannot be negative."
            );
        }

        PurchaseInvoiceId = purchaseInvoiceId;
        ItemId = itemId;
        ExpenseCategoryId = expenseCategoryId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatCategoryId = vatCategoryId;
        VatRatePercent = vatRatePercent;
        DeductibleFlag = deductibleFlag;
        CostCenterId = costCenterId == Guid.Empty ? null : costCenterId;

        Recompute();
    }

    public void Recompute()
    {
        var subtotal = decimal.Round(Quantity * UnitPrice.Amount, 4, MidpointRounding.ToEven);
        var vat = decimal.Round(subtotal * (VatRatePercent / 100m), 4, MidpointRounding.ToEven);
        LineSubtotal = MoneyEgp.From(decimal.Round(subtotal, 2, MidpointRounding.ToEven));
        LineVat = MoneyEgp.From(decimal.Round(vat, 2, MidpointRounding.ToEven));
        LineTotal = MoneyEgp.From(decimal.Round(subtotal + vat, 2, MidpointRounding.ToEven));
    }
}
