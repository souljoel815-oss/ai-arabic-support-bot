using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Quotations;

/// <summary>
/// L1.5 (v3 roadmap) — sales-pipeline quotation aggregate. NOT a
/// fiscal document: doesn't post to ETA, doesn't appear in tax
/// returns, doesn't generate journal entries. The accounting-side
/// effect happens only when the operator converts an accepted
/// quotation into a SalesInvoice (which then posts normally).
///
/// State machine:
///   Draft → Sent → Accepted → (Converted via separate action)
///   Draft → Sent → Rejected
///   Draft → Sent → Expired (auto-fired by daily job once
///                           ValidUntilDate has passed)
///   Draft → (deleted; no audit trail required for unsent drafts)
///
/// Number allocation is loose (NOT FR-011 gap-free). The
/// QuotationNumber is "QUO-YYYY-####" assigned at the
/// Draft → Sent transition by counting existing same-year
/// quotations + 1. Race-conditions possible but acceptable: missed
/// numbers in the customer-facing log are not a regulatory issue
/// for non-fiscal documents.
/// </summary>
public sealed class Quotation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public CustomerTaxProfile CustomerTaxProfileSnapshot { get; init; }

    public DateOnly DocumentDate { get; private set; }
    public DateOnly ValidUntilDate { get; private set; }
    public QuotationState State { get; private set; } = QuotationState.Draft;

    /// <summary>QUO-YYYY-#### per year. Null until Sent.</summary>
    public string? QuotationNumber { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public DateTime? ExpiredAtUtc { get; private set; }

    /// <summary>Set after the operator clicks "Convert to invoice" on an Accepted quote.</summary>
    public Guid? ConvertedToInvoiceId { get; private set; }
    public DateTime? ConvertedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }
    /// <summary>Free-text shown to the customer; e.g. "Discounts apply if accepted within 7 days".</summary>
    public string? Notes { get; private set; }

    public MoneyEgp Subtotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp NetBeforeVat { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp VatTotal { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp GrandTotal { get; private set; } = MoneyEgp.Zero;

    private readonly List<QuotationLine> _lines = new();
    public IReadOnlyCollection<QuotationLine> Lines => _lines;

    private Quotation() { }

    private Quotation(
        Guid customerId,
        CustomerTaxProfile customerTaxProfileSnapshot,
        DateOnly documentDate,
        DateOnly validUntilDate)
    {
        CustomerId = customerId;
        CustomerTaxProfileSnapshot = customerTaxProfileSnapshot;
        DocumentDate = documentDate;
        ValidUntilDate = validUntilDate;
    }

    public static Quotation CreateDraft(
        Guid customerId,
        CustomerTaxProfile customerTaxProfileSnapshot,
        DateOnly documentDate,
        DateOnly validUntilDate,
        Guid? createdByUserId = null,
        string? notes = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        if (validUntilDate < documentDate)
            throw new ArgumentException(
                "ValidUntilDate must be on or after DocumentDate.", nameof(validUntilDate));

        return new Quotation(customerId, customerTaxProfileSnapshot, documentDate, validUntilDate)
        {
            CreatedByUserId = createdByUserId,
            Notes = notes,
        };
    }

    public QuotationLine AddLine(
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent)
    {
        if (State != QuotationState.Draft)
            throw new InvalidOperationException(
                $"Cannot add a line to quotation {Id}: current state {State} is not Draft.");

        var line = new QuotationLine(Id, itemId, quantity, unitPrice, vatCategoryId, vatRatePercent);
        _lines.Add(line);
        Recompute();
        return line;
    }

    public void RemoveLine(Guid lineId)
    {
        if (State != QuotationState.Draft)
            throw new InvalidOperationException(
                $"Cannot remove a line from quotation {Id}: current state {State} is not Draft.");
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException($"Line {lineId} not found on quotation {Id}.");
        _lines.Remove(line);
        Recompute();
    }

    public void UpdateNotes(string? notes)
    {
        if (State != QuotationState.Draft)
            throw new InvalidOperationException(
                $"Cannot update notes on quotation {Id}: current state {State} is not Draft.");
        Notes = notes;
    }

    /// <summary>
    /// Draft → Sent. Caller (application service) supplies the next
    /// per-year sequence number after counting existing same-year
    /// quotations. Format: "QUO-YYYY-####". Atomic with the email
    /// dispatch so a Send that fails partway leaves State unchanged.
    /// </summary>
    public void MarkSent(int sequenceNumberThisYear, DateTime sentAtUtc)
    {
        if (State != QuotationState.Draft)
            throw new InvalidOperationException(
                $"Cannot mark quotation {Id} as Sent: current state {State} is not Draft.");
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                $"Cannot send quotation {Id}: no line items.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequenceNumberThisYear);

        QuotationNumber = $"QUO-{DocumentDate.Year}-{sequenceNumberThisYear:D4}";
        SentAtUtc = sentAtUtc;
        State = QuotationState.Sent;
    }

    public void MarkAccepted(DateTime acceptedAtUtc)
    {
        if (State != QuotationState.Sent)
            throw new InvalidOperationException(
                $"Cannot accept quotation {Id}: current state {State} is not Sent.");
        AcceptedAtUtc = acceptedAtUtc;
        State = QuotationState.Accepted;
    }

    public void MarkRejected(DateTime rejectedAtUtc)
    {
        if (State != QuotationState.Sent)
            throw new InvalidOperationException(
                $"Cannot reject quotation {Id}: current state {State} is not Sent.");
        RejectedAtUtc = rejectedAtUtc;
        State = QuotationState.Rejected;
    }

    /// <summary>
    /// Auto-fired by the daily expiry job when ValidUntilDate has
    /// passed AND the quotation is still in Sent state. Idempotent:
    /// already-Expired or already-Accepted quotes are skipped.
    /// </summary>
    public void MarkExpired(DateTime expiredAtUtc)
    {
        if (State != QuotationState.Sent) return;
        ExpiredAtUtc = expiredAtUtc;
        State = QuotationState.Expired;
    }

    /// <summary>
    /// Records that this quotation was converted into a SalesInvoice.
    /// Called by the application service AFTER it creates the invoice
    /// from this quote's lines. Caller is responsible for the actual
    /// SalesInvoice creation.
    /// </summary>
    public void RecordConvertedToInvoice(Guid invoiceId, DateTime convertedAtUtc)
    {
        if (State != QuotationState.Accepted)
            throw new InvalidOperationException(
                $"Cannot convert quotation {Id}: current state {State} is not Accepted.");
        if (ConvertedToInvoiceId is not null)
            throw new InvalidOperationException(
                $"Quotation {Id} has already been converted to invoice {ConvertedToInvoiceId}.");
        ConvertedToInvoiceId = invoiceId;
        ConvertedAtUtc = convertedAtUtc;
    }

    private void Recompute()
    {
        var subtotal = _lines.Sum(l => l.LineSubtotal.Amount);
        var net = _lines.Sum(l => l.LineNetBeforeVat.Amount);
        var vat = _lines.Sum(l => l.LineVat.Amount);
        Subtotal = MoneyEgp.From(subtotal);
        NetBeforeVat = MoneyEgp.From(net);
        VatTotal = MoneyEgp.From(vat);
        GrandTotal = MoneyEgp.From(net + vat);
    }
}

public enum QuotationState
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4,
}
