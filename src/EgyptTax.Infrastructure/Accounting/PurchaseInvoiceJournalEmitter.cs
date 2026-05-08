using EgyptTax.Application.Accounting;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;

namespace EgyptTax.Infrastructure.Accounting;

/// <summary>
/// US4 / FR-016 / FR-020 — buy-side mirror of
/// <see cref="SalesInvoiceJournalEmitter"/>. Builds the canonical
/// purchase-invoice journal:
///
///   * For deductible lines: DR Expense (line subtotal) +
///     DR InputVAT (line vat) — the input VAT lands in
///     <see cref="ChartOfAccountCodes.InputVatRecoverable"/> so it
///     reduces the period's net VAT payable to ETA.
///   * For non-deductible lines: DR Expense (line subtotal + line
///     vat) — the non-recoverable VAT is sunk into the expense.
///   * One CR AP closes the entry at the grand total.
///
/// Lines aggregate per-account so the journal stays compact: 5,000
/// lines on the source invoice still produce a 3-row journal
/// (Expense + InputVAT + AP) when everything's deductible, or 2
/// rows (Expense + AP) when nothing is.
/// </summary>
public sealed class PurchaseInvoiceJournalEmitter : IPurchaseInvoiceJournalEmitter
{
    private readonly AppDbContext _db;

    public PurchaseInvoiceJournalEmitter(AppDbContext db)
    {
        _db = db;
    }

    public Task EmitForPurchaseInvoiceAsync(
        PurchaseInvoice invoice,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (invoice.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for purchase invoice {invoice.Id}: state is {invoice.State}, not Posted."
            );
        }
        if (string.IsNullOrWhiteSpace(invoice.DocumentNumber))
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for purchase invoice {invoice.Id}: document number is empty."
            );
        }

        // Aggregate per-account so the emitted journal stays compact
        // even for invoices with hundreds of lines.
        decimal expenseDebit = 0m;
        decimal inputVatDebit = 0m;

        foreach (var line in invoice.Lines)
        {
            if (line.DeductibleFlag)
            {
                expenseDebit += line.LineSubtotal.Amount;
                inputVatDebit += line.LineVat.Amount;
            }
            else
            {
                // Non-recoverable VAT becomes part of the expense.
                expenseDebit += line.LineSubtotal.Amount + line.LineVat.Amount;
            }
        }

        var apCredit = invoice.GrandTotal.Amount;

        // Build the line list. InputVAT row is OMITTED when the
        // entire invoice is non-deductible — emitting a zero-amount
        // line would violate the JournalEntryLine "debit XOR credit
        // both non-zero" invariant.
        var lines = new List<(
            string AccountCode,
            MoneyEgp Debit,
            MoneyEgp Credit,
            string Description
        )>(3)
        {
            (
                ChartOfAccountCodes.GenericExpense,
                MoneyEgp.From(decimal.Round(expenseDebit, 2, MidpointRounding.ToEven)),
                MoneyEgp.Zero,
                $"Purchase {invoice.DocumentNumber} — book expense"
            ),
        };

        if (inputVatDebit > 0m)
        {
            lines.Add(
                (
                    ChartOfAccountCodes.InputVatRecoverable,
                    MoneyEgp.From(decimal.Round(inputVatDebit, 2, MidpointRounding.ToEven)),
                    MoneyEgp.Zero,
                    $"Purchase {invoice.DocumentNumber} — recoverable input VAT"
                )
            );
        }

        lines.Add(
            (
                ChartOfAccountCodes.AccountsPayable,
                MoneyEgp.Zero,
                MoneyEgp.From(decimal.Round(apCredit, 2, MidpointRounding.ToEven)),
                $"Purchase {invoice.DocumentNumber} — accrue payable to supplier"
            )
        );

        var entry = JournalEntry.Create(
            sourceDocumentId: invoice.Id,
            sourceDocumentNumber: invoice.DocumentNumber!,
            sourceDocumentType: DocumentType.PurchaseInvoice,
            postedAtUtc: postedAtUtc,
            lines: lines
        );

        _db.Add(entry);
        return Task.CompletedTask;
    }
}
