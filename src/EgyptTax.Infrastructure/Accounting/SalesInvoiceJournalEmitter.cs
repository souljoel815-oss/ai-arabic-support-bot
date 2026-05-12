using EgyptTax.Application.Accounting;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;

namespace EgyptTax.Infrastructure.Accounting;

/// <summary>
/// T095 — builds the canonical 3-line journal for a posted
/// SalesInvoice (or its CreditNote sibling):
///
///   * DEBIT  Accounts Receivable (1200) ← grand total
///   * CREDIT Sales Revenue        (4000) ← net-of-discount subtotal
///   * CREDIT Output VAT Payable   (2110) ← VAT total
///
/// For a credit note (<see cref="SalesInvoice.IsCreditNote"/> = true)
/// the totals on the aggregate are already negative by construction
/// (negated quantities → negative subtotals), so the emitter takes
/// the magnitudes and flips the debit/credit assignment: AR is
/// credited, Revenue is debited, Output VAT is debited. This keeps
/// the values on the rows non-negative (per the
/// <see cref="JournalEntryLine"/> constructor invariant) while
/// preserving the books-level meaning.
/// </summary>
public sealed class SalesInvoiceJournalEmitter : IJournalEntryEmitter
{
    private readonly AppDbContext _db;

    public SalesInvoiceJournalEmitter(AppDbContext db)
    {
        _db = db;
    }

    public Task EmitForSalesInvoiceAsync(
        SalesInvoice invoice,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (invoice.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for invoice {invoice.Id}: state is {invoice.State}, not Posted."
            );
        }
        if (string.IsNullOrWhiteSpace(invoice.DocumentNumber))
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for invoice {invoice.Id}: document number is empty."
            );
        }

        // Take magnitudes — sign meaning is conveyed by the
        // debit-vs-credit position, not by the amount sign.
        var arAmount = MoneyEgp.From(Math.Abs(invoice.GrandTotal.Amount));
        var revenueAmount = MoneyEgp.From(Math.Abs(invoice.NetBeforeVat.Amount));
        var vatAmount = MoneyEgp.From(Math.Abs(invoice.VatTotal.Amount));

        // EXEMPT (0%) and zero-rated invoices have no VAT — emit only
        // the AR + Revenue legs, otherwise the JournalEntryLine ctor
        // refuses the all-zeros VAT row ("a journal line MUST be
        // either a debit or a credit"). Pre-Gux.13 this branch threw
        // and surfaced as Blazor's "An unhandled error has occurred"
        // overlay — see BUG-004 in the May 2026 testing report.
        var hasVat = vatAmount.Amount > 0m;
        var lineList = new List<(string, MoneyEgp, MoneyEgp, string)>(3);

        if (invoice.IsCreditNote)
        {
            lineList.Add((
                ChartOfAccountCodes.AccountsReceivable,
                MoneyEgp.Zero,
                arAmount,
                $"Credit note {invoice.DocumentNumber} — release receivable"));
            lineList.Add((
                ChartOfAccountCodes.SalesRevenue,
                revenueAmount,
                MoneyEgp.Zero,
                $"Credit note {invoice.DocumentNumber} — reverse revenue"));
            if (hasVat)
            {
                lineList.Add((
                    ChartOfAccountCodes.OutputVatPayable,
                    vatAmount,
                    MoneyEgp.Zero,
                    $"Credit note {invoice.DocumentNumber} — claw back output VAT"));
            }
        }
        else
        {
            lineList.Add((
                ChartOfAccountCodes.AccountsReceivable,
                arAmount,
                MoneyEgp.Zero,
                $"Invoice {invoice.DocumentNumber} — book receivable"));
            lineList.Add((
                ChartOfAccountCodes.SalesRevenue,
                MoneyEgp.Zero,
                revenueAmount,
                $"Invoice {invoice.DocumentNumber} — recognise revenue"));
            if (hasVat)
            {
                lineList.Add((
                    ChartOfAccountCodes.OutputVatPayable,
                    MoneyEgp.Zero,
                    vatAmount,
                    $"Invoice {invoice.DocumentNumber} — accrue output VAT"));
            }
        }
        var lines = lineList;

        var documentType = invoice.IsCreditNote
            ? DocumentType.CreditNote
            : DocumentType.SalesInvoice;

        var entry = JournalEntry.Create(
            sourceDocumentId: invoice.Id,
            sourceDocumentNumber: invoice.DocumentNumber!,
            sourceDocumentType: documentType,
            postedAtUtc: postedAtUtc,
            lines: lines
        );

        _db.Add(entry);
        return Task.CompletedTask;
    }
}
