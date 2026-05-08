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

    /// <summary>
    /// FR-008 expansion — slice of the invoice-level discount this
    /// line absorbs. Set by <see cref="SalesInvoice.Recompute"/> only;
    /// the apportionment policy is "pro-rata by line subtotal" with
    /// the rounding remainder landing on the last line so
    /// <c>sum(LineApportionedDiscount) == invoice.InvoiceLevelDiscountAmount</c>
    /// to the cent.
    /// </summary>
    public MoneyEgp LineApportionedDiscount { get; private set; } = MoneyEgp.Zero;

    /// <summary>
    /// Line subtotal AFTER the apportioned invoice-level discount —
    /// equal to <see cref="LineSubtotal"/> minus
    /// <see cref="LineApportionedDiscount"/>. This is the basis VAT
    /// is computed against.
    /// </summary>
    public MoneyEgp LineNetSubtotal { get; private set; } = MoneyEgp.Zero;

    public MoneyEgp LineVat { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp LineTotal { get; private set; } = MoneyEgp.Zero;

    private SalesInvoiceLine() { }

    public SalesInvoiceLine(
        Guid salesInvoiceId,
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent
    )
    {
        if (quantity == 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Line quantity must be non-zero."
            );
        }
        // Negative quantities are valid for credit-note lines per
        // FR-013 (signs reversed against the original); the
        // SalesInvoice aggregate validates sign-consistency at the
        // header level.
        if (unitPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Line unit price cannot be negative."
            );
        }
        if (vatRatePercent < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vatRatePercent),
                "VAT rate cannot be negative."
            );
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
    /// Recompute the line totals from its inputs (and the current
    /// <see cref="LineApportionedDiscount"/>). Banker's rounding per
    /// <see cref="MoneyEgp.AmountRoundedToCents"/> ensures the stored
    /// value matches the displayed and audited value.
    /// </summary>
    public void Recompute()
    {
        var subtotal = decimal.Round(Quantity * UnitPrice.Amount, 4, MidpointRounding.ToEven);
        var netSubtotal = subtotal - LineApportionedDiscount.Amount;
        var vat = decimal.Round(netSubtotal * (VatRatePercent / 100m), 4, MidpointRounding.ToEven);
        var total = netSubtotal + vat;
        LineSubtotal = MoneyEgp.From(decimal.Round(subtotal, 2, MidpointRounding.ToEven));
        LineNetSubtotal = MoneyEgp.From(decimal.Round(netSubtotal, 2, MidpointRounding.ToEven));
        LineVat = MoneyEgp.From(decimal.Round(vat, 2, MidpointRounding.ToEven));
        LineTotal = MoneyEgp.From(decimal.Round(total, 2, MidpointRounding.ToEven));
    }

    /// <summary>
    /// Internal seam used by <see cref="SalesInvoice.Recompute"/> to
    /// set the apportioned slice of the invoice-level discount and
    /// then recompute the line totals against the new net subtotal.
    /// </summary>
    internal void SetApportionedDiscount(MoneyEgp apportioned)
    {
        if (apportioned.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(apportioned),
                "Apportioned line discount cannot be negative."
            );
        }
        LineApportionedDiscount = apportioned;
        Recompute();
    }
}
