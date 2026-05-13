using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Sales;

/// <summary>
/// Phase F — Sales Order. A pre-invoice quote/order taken by a sales
/// rep in the field (often before stock is confirmed or before the
/// office can run the credit-limit + tax checks). Independent of the
/// FR-011 numbering allocator and the ETA pipeline because orders are
/// NOT tax-impacting documents — they're just the rep's pipeline.
///
/// Lifecycle:
///   Draft     → still being edited
///   Confirmed → rep submitted; ready for the office to convert
///   Converted → office created a SalesInvoice from this order
///               (one-way; orders can't be re-converted)
///   Cancelled → terminal; the order won't become an invoice
///
/// Conversion creates a new SalesInvoice draft using the order's
/// customer + lines. The post-time guards (credit limit, stock,
/// approval) all run on the resulting invoice — the order itself
/// never decrements stock or hits the customer's receivable.
/// </summary>
public sealed class SalesOrder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OrderNumber { get; private set; } = "";
    public Guid CustomerId { get; init; }
    public DateOnly OrderDate { get; private set; }
    public SalesOrderState State { get; private set; } = SalesOrderState.Draft;
    public string? Note { get; private set; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? ConvertedAtUtc { get; private set; }

    /// <summary>
    /// Back-pointer to the SalesInvoice the office created from this
    /// order. Set by ConvertToInvoice; the list view uses it to render
    /// "→ INV-2026-000123" instead of a "Convert" button.
    /// </summary>
    public Guid? ConvertedToInvoiceId { get; private set; }

    private readonly List<SalesOrderLine> _lines = new();
    public IReadOnlyCollection<SalesOrderLine> Lines => _lines;

    public MoneyEgp Subtotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp VatTotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp GrandTotal { get; private set; } = MoneyEgp.Zero;

    private SalesOrder() { }

    private SalesOrder(Guid customerId, DateOnly orderDate, Guid? createdByUserId)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        CustomerId = customerId;
        OrderDate = orderDate;
        CreatedByUserId = createdByUserId;
    }

    public static SalesOrder CreateDraft(Guid customerId, DateOnly orderDate, Guid? createdByUserId = null)
        => new(customerId, orderDate, createdByUserId);

    public void AddLine(Guid itemId, decimal quantity, MoneyEgp unitPrice, Guid vatCategoryId, decimal vatRatePercent)
    {
        EnsureMutable();
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        _lines.Add(new SalesOrderLine(itemId, quantity, unitPrice, vatCategoryId, vatRatePercent));
        Recompute();
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureMutable();
        _lines.RemoveAll(l => l.Id == lineId);
        Recompute();
    }

    public void UpdateNote(string? note)
    {
        EnsureMutable();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void Confirm(string orderNumber, DateTime nowUtc)
    {
        if (State != SalesOrderState.Draft)
            throw new InvalidOperationException($"Cannot confirm order in state {State}.");
        if (_lines.Count == 0)
            throw new InvalidOperationException("Cannot confirm an empty order — add at least one line first.");
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        OrderNumber = orderNumber;
        State = SalesOrderState.Confirmed;
        ConfirmedAtUtc = nowUtc;
    }

    public void MarkConverted(Guid invoiceId, DateTime nowUtc)
    {
        if (State != SalesOrderState.Confirmed)
            throw new InvalidOperationException(
                $"Only Confirmed orders can be converted to invoices. This order is {State}.");
        ConvertedToInvoiceId = invoiceId;
        ConvertedAtUtc = nowUtc;
        State = SalesOrderState.Converted;
    }

    public void Cancel()
    {
        if (State == SalesOrderState.Converted)
            throw new InvalidOperationException(
                "Cannot cancel an order that's already been converted to an invoice. Void the invoice instead.");
        State = SalesOrderState.Cancelled;
    }

    private void EnsureMutable()
    {
        if (State != SalesOrderState.Draft)
            throw new InvalidOperationException($"Order is {State} and can no longer be edited.");
    }

    private void Recompute()
    {
        var sub = 0m; var vat = 0m;
        foreach (var l in _lines)
        {
            var lineSub = decimal.Round(l.Quantity * l.UnitPrice.Amount, 2, MidpointRounding.ToEven);
            var lineVat = decimal.Round(lineSub * (l.VatRatePercent / 100m), 2, MidpointRounding.ToEven);
            sub += lineSub;
            vat += lineVat;
        }
        Subtotal = MoneyEgp.From(sub);
        VatTotal = MoneyEgp.From(vat);
        GrandTotal = MoneyEgp.From(sub + vat);
    }
}

public sealed class SalesOrderLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SalesOrderId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public MoneyEgp UnitPrice { get; init; }
    public Guid VatCategoryId { get; init; }
    public decimal VatRatePercent { get; init; }

    private SalesOrderLine() { UnitPrice = MoneyEgp.Zero; }

    public SalesOrderLine(Guid itemId, decimal quantity, MoneyEgp unitPrice, Guid vatCategoryId, decimal vatRatePercent)
    {
        if (itemId == Guid.Empty) throw new ArgumentException("ItemId required.", nameof(itemId));
        ItemId = itemId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatCategoryId = vatCategoryId;
        VatRatePercent = vatRatePercent;
    }
}

public enum SalesOrderState
{
    Draft,
    Confirmed,
    Converted,
    Cancelled,
}
