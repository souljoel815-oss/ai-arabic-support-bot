using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Purchases;

/// <summary>
/// C2 — purchase invoice aggregate root per FR-009 / FR-016 /
/// FR-020 / FR-041. Mirrors the FR-026 state machine that
/// SalesInvoice already proves out (Draft → [Submitted → Approved
/// →] Posted) and snapshots the supplier's tax profile at post-time
/// so input-VAT recoverability decisions survive later master-data
/// edits.
///
/// Notable differences from SalesInvoice:
///  * <see cref="SupplierInvoiceNumber"/> is the number printed on
///    the supplier's PDF, distinct from the (FR-011) document number
///    we allocate locally on Post. Carries the dedup fingerprint for
///    the T142 DuplicateSupplierInvoiceRule.
///  * Lines carry the <see cref="PurchaseInvoiceLine.DeductibleFlag"/>
///    + an XOR(item, expense_category) discriminator — purchase docs
///    can mix inventory and expense lines.
///  * No invoice-level discount in the MVP slice (the spec doesn't
///    require it for the buy side; the FR-008 expansion was a
///    sales-side scope).
///  * No QR seal / no ETA submission row — purchase invoices come
///    FROM the supplier, the buyer doesn't submit them to ETA.
/// </summary>
public sealed class PurchaseInvoice
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SupplierId { get; init; }
    public SupplierTaxProfile SupplierTaxProfileSnapshot { get; init; }

    public string SupplierInvoiceNumber { get; private set; } = "";
    public DateOnly DateReceived { get; private set; }
    public DocumentState State { get; private set; } = DocumentState.Draft;

    public string? DocumentNumber { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public DocumentPostingMode? PostingMode { get; private set; }

    public MoneyEgp Subtotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp VatTotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp GrandTotal { get; private set; } = MoneyEgp.Zero;

    private readonly List<PurchaseInvoiceLine> _lines = new();
    public IReadOnlyCollection<PurchaseInvoiceLine> Lines => _lines;

    private PurchaseInvoice() { }

    private PurchaseInvoice(
        Guid supplierId,
        SupplierTaxProfile supplierTaxProfileSnapshot,
        string supplierInvoiceNumber,
        DateOnly dateReceived)
    {
        SupplierId = supplierId;
        SupplierTaxProfileSnapshot = supplierTaxProfileSnapshot;
        SupplierInvoiceNumber = supplierInvoiceNumber;
        DateReceived = dateReceived;
    }

    public static PurchaseInvoice CreateDraft(
        Guid supplierId,
        SupplierTaxProfile supplierTaxProfileSnapshot,
        string supplierInvoiceNumber,
        DateOnly dateReceived)
    {
        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("SupplierId is required.", nameof(supplierId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierInvoiceNumber);
        return new PurchaseInvoice(supplierId, supplierTaxProfileSnapshot, supplierInvoiceNumber, dateReceived);
    }

    public PurchaseInvoiceLine AddLine(
        Guid? itemId,
        Guid? expenseCategoryId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent,
        bool deductibleFlag)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot add a line to purchase invoice {Id}: current state {State} is not Draft.");
        }
        var line = new PurchaseInvoiceLine(
            Id, itemId, expenseCategoryId, quantity, unitPrice,
            vatCategoryId, vatRatePercent, deductibleFlag);
        _lines.Add(line);
        Recompute();
        return line;
    }

    public void RemoveLine(Guid lineId)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot remove a line from purchase invoice {Id}: current state {State} is not Draft.");
        }
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException($"Line {lineId} is not on purchase invoice {Id}.");
        _lines.Remove(line);
        Recompute();
    }

    public void UpdateSupplierInvoiceNumber(string supplierInvoiceNumber)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot edit supplier invoice number on purchase invoice {Id}: state {State} is not Draft.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierInvoiceNumber);
        SupplierInvoiceNumber = supplierInvoiceNumber;
    }

    public void UpdateDateReceived(DateOnly dateReceived)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot edit date received on purchase invoice {Id}: state {State} is not Draft.");
        }
        DateReceived = dateReceived;
    }

    public void Recompute()
    {
        var subtotal = _lines.Sum(l => l.LineSubtotal.Amount);
        var vat = _lines.Sum(l => l.LineVat.Amount);
        Subtotal = MoneyEgp.From(decimal.Round(subtotal, 2, MidpointRounding.ToEven));
        VatTotal = MoneyEgp.From(decimal.Round(vat, 2, MidpointRounding.ToEven));
        GrandTotal = MoneyEgp.From(decimal.Round(subtotal + vat, 2, MidpointRounding.ToEven));
    }

    public void MarkPosted(
        string documentNumber,
        Guid postedByUserId,
        DateTime postedAtUtc,
        DocumentPostingMode postingMode,
        bool approvalEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        var nextState = DocumentStateMachine.Transition(State, DocumentState.Posted, approvalEnabled);
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot post purchase invoice {Id}: at least one line is required.");
        }
        Recompute();
        DocumentNumber = documentNumber;
        PostedByUserId = postedByUserId;
        PostedAtUtc = postedAtUtc;
        PostingMode = postingMode;
        State = nextState;
    }
}
