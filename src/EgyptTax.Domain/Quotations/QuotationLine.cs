using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Quotations;

/// <summary>
/// L1.5 — line item on a quotation. Mirrors SalesInvoiceLine
/// shape so convert-to-invoice maps fields 1:1. Discount /
/// per-line VAT-exemption are intentionally absent in v1; the
/// v3 plan keeps quotations a "simple offer document" — anything
/// fancier lands when a real customer asks.
/// </summary>
public sealed class QuotationLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid QuotationId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public MoneyEgp UnitPrice { get; init; } = MoneyEgp.Zero;
    public Guid VatCategoryId { get; init; }
    public decimal VatRatePercent { get; init; }

    public MoneyEgp LineSubtotal => MoneyEgp.From(Quantity * UnitPrice.Amount);
    public MoneyEgp LineNetBeforeVat => LineSubtotal;
    public MoneyEgp LineVat =>
        MoneyEgp.From(Math.Round(LineNetBeforeVat.Amount * VatRatePercent / 100m, 2, MidpointRounding.AwayFromZero));

    private QuotationLine() { }

    public QuotationLine(
        Guid quotationId,
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent)
    {
        if (quotationId == Guid.Empty)
            throw new ArgumentException("QuotationId is required.", nameof(quotationId));
        if (itemId == Guid.Empty)
            throw new ArgumentException("ItemId is required.", nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (vatRatePercent < 0)
            throw new ArgumentOutOfRangeException(nameof(vatRatePercent), "VAT rate cannot be negative.");

        QuotationId = quotationId;
        ItemId = itemId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatCategoryId = vatCategoryId;
        VatRatePercent = vatRatePercent;
    }
}
