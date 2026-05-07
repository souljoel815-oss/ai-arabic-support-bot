using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Invoices;

/// <summary>
/// C1 — Sales invoice aggregate root. Encapsulates the FR-026 state
/// machine (Draft → [Submitted → Approved →] Posted), the FR-040
/// CustomerTaxProfile snapshot, and the per-post total recomputation.
/// Document number allocation, posting timestamp + actor, and audit
/// emission are all driven by the application-layer
/// <c>PostSalesInvoiceHandler</c> rather than the entity itself; the
/// entity exposes <see cref="MarkPosted"/> as the single mutating
/// transition into the terminal state, and <see cref="Recompute"/> for
/// the totals roll-up.
/// </summary>
public sealed class SalesInvoice
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public CustomerTaxProfile CustomerTaxProfileSnapshot { get; init; }

    public DateOnly DocumentDate { get; private set; }
    public DocumentState State { get; private set; } = DocumentState.Draft;

    public string? DocumentNumber { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public DocumentPostingMode? PostingMode { get; private set; }

    public MoneyEgp Subtotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp VatTotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp GrandTotal { get; private set; } = MoneyEgp.Zero;

    private readonly List<SalesInvoiceLine> _lines = new();
    public IReadOnlyCollection<SalesInvoiceLine> Lines => _lines;

    private SalesInvoice() { }

    private SalesInvoice(
        Guid customerId,
        CustomerTaxProfile customerTaxProfileSnapshot,
        DateOnly documentDate)
    {
        CustomerId = customerId;
        CustomerTaxProfileSnapshot = customerTaxProfileSnapshot;
        DocumentDate = documentDate;
    }

    public static SalesInvoice CreateDraft(
        Guid customerId,
        CustomerTaxProfile customerTaxProfileSnapshot,
        DateOnly documentDate)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }
        return new SalesInvoice(customerId, customerTaxProfileSnapshot, documentDate);
    }

    public SalesInvoiceLine AddLine(
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot add a line to sales invoice {Id}: current state {State} is not Draft.");
        }
        var line = new SalesInvoiceLine(Id, itemId, quantity, unitPrice, vatCategoryId, vatRatePercent);
        _lines.Add(line);
        Recompute();
        return line;
    }

    public void Recompute()
    {
        var subtotal = 0m;
        var vat = 0m;
        foreach (var line in _lines)
        {
            line.Recompute();
            subtotal += line.LineSubtotal.Amount;
            vat += line.LineVat.Amount;
        }
        Subtotal = MoneyEgp.From(decimal.Round(subtotal, 2, MidpointRounding.ToEven));
        VatTotal = MoneyEgp.From(decimal.Round(vat, 2, MidpointRounding.ToEven));
        GrandTotal = MoneyEgp.From(decimal.Round(subtotal + vat, 2, MidpointRounding.ToEven));
    }

    /// <summary>
    /// FR-026 — terminal post transition. Caller (the Application
    /// handler) is responsible for resolving the document number,
    /// the actor's user id, and the
    /// <see cref="DocumentPostingMode"/> per the
    /// <c>DocumentTypeApprovalSetting</c> lookup; this method only
    /// transitions the state and persists those values.
    /// </summary>
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
                $"Cannot post sales invoice {Id}: at least one line is required.");
        }

        Recompute();
        DocumentNumber = documentNumber;
        PostedByUserId = postedByUserId;
        PostedAtUtc = postedAtUtc;
        PostingMode = postingMode;
        State = nextState;
    }
}
