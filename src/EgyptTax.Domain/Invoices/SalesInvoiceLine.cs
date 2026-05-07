using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Invoices;

/// <summary>
/// One billable line on a sales invoice. Computed totals
/// (<see cref="LineSubtotal"/>, <see cref="LineVat"/>,
/// <see cref="LineTotal"/>) live on the row so the post-time snapshot
/// is preserved verbatim; later master-data corrections to the VAT
/// rate or the unit price do NOT silently revise the historical
/// invoice.
/// </summary>
public sealed class SalesInvoiceLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SalesInvoiceId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public MoneyEgp UnitPrice { get; init; }
    public Guid VatCategoryId { get; init; }

    /// <summary>
    /// VAT rate active on the document_date — captured here at line
    /// creation so the recompute on post is deterministic against the
    /// rate that was in force per FR-022.
    /// </summary>
    public decimal VatRatePercent { get; init; }

    public MoneyEgp LineSubtotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp LineVat { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp LineTotal { get; private set; } = MoneyEgp.Zero;

    private SalesInvoiceLine() { }

    public SalesInvoiceLine(
        Guid salesInvoiceId,
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Line quantity must be positive.");
        }
        if (unitPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Line unit price cannot be negative.");
        }
        if (vatRatePercent < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRatePercent), "VAT rate cannot be negative.");
        }

        SalesInvoiceId = salesInvoiceId;
        ItemId = itemId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatCategoryId = vatCategoryId;
        VatRatePercent = vatRatePercent;
        Recompute();
    }

    /// <summary>
    /// Recompute the line totals from its inputs. Banker's rounding
    /// per <see cref="MoneyEgp.AmountRoundedToCents"/> ensures the
    /// stored value matches the displayed and audited value.
    /// </summary>
    public void Recompute()
    {
        var subtotal = decimal.Round(Quantity * UnitPrice.Amount, 4, MidpointRounding.ToEven);
        var vat = decimal.Round(subtotal * (VatRatePercent / 100m), 4, MidpointRounding.ToEven);
        var total = subtotal + vat;
        LineSubtotal = MoneyEgp.From(decimal.Round(subtotal, 2, MidpointRounding.ToEven));
        LineVat = MoneyEgp.From(decimal.Round(vat, 2, MidpointRounding.ToEven));
        LineTotal = MoneyEgp.From(decimal.Round(total, 2, MidpointRounding.ToEven));
    }
}
