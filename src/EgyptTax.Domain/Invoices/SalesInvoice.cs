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

    /// <summary>
    /// FR-013 — when non-null, this document is a CreditNote
    /// referencing the original SalesInvoice. The
    /// <see cref="IsCreditNote"/> getter is the canonical predicate;
    /// numbering allocation, document-type label on the PDF, and the
    /// sign-consistency rule on lines all branch on it.
    /// </summary>
    public Guid? CreditNoteOfInvoiceId { get; private set; }

    /// <summary>FR-013 — free-text reason captured at issue time.</summary>
    public string? CreditNoteReason { get; private set; }

    public bool IsCreditNote => CreditNoteOfInvoiceId.HasValue;

    public MoneyEgp Subtotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp InvoiceLevelDiscountAmount { get; private set; } = MoneyEgp.Zero;
    public decimal InvoiceLevelDiscountPercent { get; private set; }
    public MoneyEgp NetBeforeVat { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp VatTotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp GrandTotal { get; private set; } = MoneyEgp.Zero;

    private readonly List<SalesInvoiceLine> _lines = new();
    public IReadOnlyCollection<SalesInvoiceLine> Lines => _lines;

    private SalesInvoice() { }

    private SalesInvoice(
        Guid customerId,
        CustomerTaxProfile customerTaxProfileSnapshot,
        DateOnly documentDate
    )
    {
        CustomerId = customerId;
        CustomerTaxProfileSnapshot = customerTaxProfileSnapshot;
        DocumentDate = documentDate;
    }

    public static SalesInvoice CreateDraft(
        Guid customerId,
        CustomerTaxProfile customerTaxProfileSnapshot,
        DateOnly documentDate
    )
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }
        return new SalesInvoice(customerId, customerTaxProfileSnapshot, documentDate);
    }

    /// <summary>
    /// FR-013 — factory for a credit note that corrects an
    /// existing posted SalesInvoice. The original's customer +
    /// CustomerTaxProfile snapshot are copied so the credit note
    /// references exactly the same parties; the caller adds lines
    /// with negated quantities via <see cref="AddLine"/>. The
    /// <c>reason</c> is the legally-required justification for
    /// the correction and is non-empty per FR-013.
    /// </summary>
    public static SalesInvoice CreateCreditNoteFor(
        SalesInvoice originalInvoice,
        string reason,
        DateOnly documentDate
    )
    {
        ArgumentNullException.ThrowIfNull(originalInvoice);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (originalInvoice.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot issue a credit note against {originalInvoice.Id}: source state is {originalInvoice.State}, not Posted. FR-013 + FR-027 require the source to be Posted."
            );
        }
        if (originalInvoice.IsCreditNote)
        {
            throw new InvalidOperationException(
                $"Cannot issue a credit note against {originalInvoice.Id}: source is itself a credit note. Credit-note-of-credit-note is non-sensical and would unwind the audit trail."
            );
        }

        var draft = new SalesInvoice(
            originalInvoice.CustomerId,
            originalInvoice.CustomerTaxProfileSnapshot,
            documentDate
        );
        draft.CreditNoteOfInvoiceId = originalInvoice.Id;
        draft.CreditNoteReason = reason;
        return draft;
    }

    public SalesInvoiceLine AddLine(
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent
    )
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot add a line to sales invoice {Id}: current state {State} is not Draft."
            );
        }
        var line = new SalesInvoiceLine(
            Id,
            itemId,
            quantity,
            unitPrice,
            vatCategoryId,
            vatRatePercent
        );
        _lines.Add(line);
        Recompute();
        return line;
    }

    /// <summary>
    /// Remove a line from a Draft invoice and recompute totals. Throws
    /// if the invoice is not Draft (consistent with AddLine) or if the
    /// supplied line id is not on this invoice. Note that the
    /// invoice-level discount apportionment automatically redistributes
    /// across the remaining lines (the rounding remainder will land on
    /// the new last line).
    /// </summary>
    public void RemoveLine(Guid lineId)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot remove a line from sales invoice {Id}: current state {State} is not Draft."
            );
        }
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            throw new InvalidOperationException($"Line {lineId} is not on sales invoice {Id}.");
        }
        _lines.Remove(line);
        Recompute();
    }

    /// <summary>
    /// FR-008 expansion — set or clear the invoice-level discount.
    /// Exactly one of <paramref name="amount"/> / <paramref name="percent"/>
    /// may be non-null; both null clears the discount; both non-null
    /// throws because the source-of-truth would be ambiguous. The
    /// discount apportions across lines pro-rata in <see cref="Recompute"/>.
    /// </summary>
    public void SetInvoiceLevelDiscount(MoneyEgp? amount, decimal? percent)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot change invoice-level discount on sales invoice {Id}: current state {State} is not Draft."
            );
        }
        if (amount is not null && percent is not null)
        {
            throw new ArgumentException(
                "Exactly one of {amount, percent} must be supplied; the other must be null.",
                nameof(amount)
            );
        }
        if (percent is { } pct && pct is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                "Invoice-level discount percent must be in the range [0, 100]."
            );
        }
        if (amount is { } amt && amt.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Invoice-level discount amount cannot be negative."
            );
        }

        InvoiceLevelDiscountAmount = amount ?? MoneyEgp.Zero;
        InvoiceLevelDiscountPercent = percent ?? 0m;
        Recompute();
    }

    public void Recompute()
    {
        // Reset apportioned discounts so the pre-discount line subtotals
        // are visible for the apportionment math.
        foreach (var line in _lines)
        {
            line.SetApportionedDiscount(MoneyEgp.Zero);
        }

        var preDiscountSubtotal = _lines.Sum(l => l.LineSubtotal.Amount);

        // Resolve effective invoice-level discount: percent takes
        // precedence over amount when both are non-zero (the only way
        // both end up non-zero is via direct field manipulation in
        // tests; SetInvoiceLevelDiscount enforces XOR).
        decimal effectiveDiscount;
        if (InvoiceLevelDiscountPercent > 0m && preDiscountSubtotal > 0m)
        {
            effectiveDiscount = decimal.Round(
                preDiscountSubtotal * (InvoiceLevelDiscountPercent / 100m),
                2,
                MidpointRounding.ToEven
            );
        }
        else
        {
            effectiveDiscount = InvoiceLevelDiscountAmount.Amount;
        }

        // The "discount exceeds subtotal" check only applies to
        // regular (positive) subtotals. For credit notes the
        // pre-discount subtotal is negative by construction; the
        // invoice-level discount is always zero on credit notes
        // (the editor doesn't expose it for them) so the comparison
        // is degenerate.
        if (effectiveDiscount > 0m && effectiveDiscount > preDiscountSubtotal)
        {
            throw new InvalidOperationException(
                $"Invoice-level discount {effectiveDiscount:F2} exceeds pre-discount subtotal {preDiscountSubtotal:F2}."
            );
        }

        // Apportion pro-rata. The last line absorbs the rounding
        // remainder so sum(apportioned) == effectiveDiscount exactly.
        if (effectiveDiscount > 0m && preDiscountSubtotal > 0m)
        {
            var allocated = 0m;
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                decimal apportioned;
                if (i == _lines.Count - 1)
                {
                    apportioned = effectiveDiscount - allocated;
                }
                else
                {
                    var ratio = line.LineSubtotal.Amount / preDiscountSubtotal;
                    apportioned = decimal.Round(
                        effectiveDiscount * ratio,
                        2,
                        MidpointRounding.ToEven
                    );
                }
                line.SetApportionedDiscount(MoneyEgp.From(apportioned));
                allocated += apportioned;
            }
        }

        // Header totals are sums of the now-discounted line totals.
        var subtotal = _lines.Sum(l => l.LineSubtotal.Amount);
        var netBeforeVat = _lines.Sum(l => l.LineNetSubtotal.Amount);
        var vat = _lines.Sum(l => l.LineVat.Amount);
        Subtotal = MoneyEgp.From(decimal.Round(subtotal, 2, MidpointRounding.ToEven));
        NetBeforeVat = MoneyEgp.From(decimal.Round(netBeforeVat, 2, MidpointRounding.ToEven));
        VatTotal = MoneyEgp.From(decimal.Round(vat, 2, MidpointRounding.ToEven));
        GrandTotal = MoneyEgp.From(decimal.Round(netBeforeVat + vat, 2, MidpointRounding.ToEven));

        // The InvoiceLevelDiscountAmount field always reflects the
        // computed effective discount so downstream consumers (eInvoice
        // generator, PDF renderer) see one number whether the source
        // was percent or fixed.
        InvoiceLevelDiscountAmount = MoneyEgp.From(effectiveDiscount);
    }

    /// <summary>
    /// FR-026 — terminal post transition. Caller (the Application
    /// handler) is responsible for resolving the document number,
    /// the actor's user id, and the
    /// <see cref="DocumentPostingMode"/> per the
    /// <c>DocumentTypeApprovalSetting</c> lookup; this method only
    /// transitions the state and persists those values.
    /// </summary>
    /// <summary>
    /// FR-026 — Bookkeeper transitions a Draft document to
    /// Submitted to send it for approval.
    /// </summary>
    public void MarkSubmitted()
    {
        State = DocumentStateMachine.Transition(
            State,
            DocumentState.Submitted,
            approvalEnabled: true
        );
    }

    /// <summary>FR-026 — Approver moves Submitted → Approved.</summary>
    public void MarkApproved()
    {
        State = DocumentStateMachine.Transition(
            State,
            DocumentState.Approved,
            approvalEnabled: true
        );
    }

    /// <summary>
    /// FR-026 — Approver rejects: Submitted → Draft. Operator
    /// edits + re-submits. The rejection reason is captured in
    /// the ApprovalRequest row by the application handler.
    /// </summary>
    public void MarkRejected()
    {
        State = DocumentStateMachine.Transition(State, DocumentState.Draft, approvalEnabled: true);
    }

    /// <summary>
    /// FR-026 — Voiding a non-Posted document. Posted documents
    /// cannot be voided per FR-027; corrections route through
    /// credit notes (FR-013).
    /// </summary>
    public void MarkVoided()
    {
        State = DocumentStateMachine.Transition(State, DocumentState.Voided, approvalEnabled: true);
    }

    public void MarkPosted(
        string documentNumber,
        Guid postedByUserId,
        DateTime postedAtUtc,
        DocumentPostingMode postingMode,
        bool approvalEnabled
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        var nextState = DocumentStateMachine.Transition(
            State,
            DocumentState.Posted,
            approvalEnabled
        );
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot post sales invoice {Id}: at least one line is required."
            );
        }

        Recompute();
        DocumentNumber = documentNumber;
        PostedByUserId = postedByUserId;
        PostedAtUtc = postedAtUtc;
        PostingMode = postingMode;
        State = nextState;
    }
}
