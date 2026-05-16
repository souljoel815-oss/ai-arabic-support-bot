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

    /// <summary>v4 C.2 — optional per-line cost-center tag.
    /// When set, the cost-centers report attributes this line's
    /// revenue (LineSubtotal) to the matching center on top of
    /// the document-level tagging that exists for expenses.
    /// Null = "untagged" (the report buckets these separately so
    /// the operator sees the data-quality gap).</summary>
    public Guid? CostCenterId { get; init; }

    /// <summary>v5 D.1 v2 — JSON-serialised array of <c>ItemSerial.Id</c>
    /// values that this line consumes. Populated when the operator picks
    /// serials on the line during invoice editing for items with
    /// <c>Item.TracksSerials = true</c>. On post, the
    /// <c>PostSalesInvoiceHandler</c> transitions each listed serial to
    /// <c>Sold</c> and back-points the customer + invoice id. Stored as
    /// JSON (not a separate child entity) because the list is short
    /// (typically 1–N where N = quantity) and never queried independently.</summary>
    public string? SerialIdsJson { get; private set; }

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
        decimal vatRatePercent,
        Guid? costCenterId = null
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
        CostCenterId = costCenterId == Guid.Empty ? null : costCenterId;
        Recompute();
    }

    /// <summary>v5 D.1 v2 — set the list of <see cref="EgyptTax.Domain.MasterData.ItemSerial"/>
    /// ids that this line consumes. Replaces the previous list verbatim.
    /// Pass <c>null</c> or an empty enumerable to clear. The aggregate
    /// stores the GUIDs as a JSON array to keep the schema flat.</summary>
    public void SetSoldSerialIds(IEnumerable<Guid>? serialIds)
    {
        if (serialIds is null)
        {
            SerialIdsJson = null;
            return;
        }
        var list = serialIds
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();
        SerialIdsJson = list.Count == 0
            ? null
            : System.Text.Json.JsonSerializer.Serialize(list);
    }

    /// <summary>v5 D.1 v2 — read the previously-stored serial ids.
    /// Returns an empty list when none were set. Caller is the
    /// <c>PostSalesInvoiceHandler</c>; the line itself never needs
    /// to reason about serial state.</summary>
    public IReadOnlyList<Guid> GetSoldSerialIds()
    {
        if (string.IsNullOrWhiteSpace(SerialIdsJson)) return Array.Empty<Guid>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(SerialIdsJson)
                ?? (IReadOnlyList<Guid>)Array.Empty<Guid>();
        }
        catch
        {
            // Corrupted or hand-edited JSON — return empty rather than
            // crash the post path. The audit log already records the
            // raw column on save.
            return Array.Empty<Guid>();
        }
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
