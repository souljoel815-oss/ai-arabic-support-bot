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
/// P1.12 — reverse-charge variant (foreign supplier, FR-041): the
/// supplier didn't charge VAT (they're outside Egypt's VAT system),
/// so we self-account: DR Input VAT + CR Output VAT for the same
/// amount. Net cash effect on the period is zero, but both totals
/// surface in the VAT return — the regulator wants to see the
/// reverse-charge flow declared, not netted away. AP is credited
/// at NetBeforeVat (we owe the supplier the net only).
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

        // P1.12 — reverse-charge mode is set on the snapshot at
        // post-time (foreign supplier => ReverseChargeFlag = true).
        // When true, we self-account the VAT instead of paying it to
        // the supplier; the AP credit covers only the net.
        var isReverseCharge = invoice.SupplierTaxProfileSnapshot.ReverseChargeFlag;

        // Aggregate per-account so the emitted journal stays compact
        // even for invoices with hundreds of lines.
        decimal expenseDebit = 0m;
        decimal inputVatDebit = 0m;
        decimal reverseChargeOutputVatCredit = 0m;

        foreach (var line in invoice.Lines)
        {
            if (line.DeductibleFlag)
            {
                expenseDebit += line.LineSubtotal.Amount;
                inputVatDebit += line.LineVat.Amount;
                if (isReverseCharge)
                {
                    // Mirror the input-VAT debit with an output-VAT
                    // credit for the same amount — that's the
                    // self-accounting leg.
                    reverseChargeOutputVatCredit += line.LineVat.Amount;
                }
            }
            else
            {
                // Non-recoverable VAT becomes part of the expense.
                expenseDebit += line.LineSubtotal.Amount + line.LineVat.Amount;
            }
        }

        // For reverse-charge, the supplier didn't bill VAT, so AP
        // covers only the net amount we actually owe them. Purchase
        // invoices don't carry invoice-level discounts so Subtotal
        // is the net.
        var apCredit = isReverseCharge
            ? invoice.Subtotal.Amount
            : invoice.GrandTotal.Amount;

        // Build the line list. InputVAT row is OMITTED when the
        // entire invoice is non-deductible — emitting a zero-amount
        // line would violate the JournalEntryLine "debit XOR credit
        // both non-zero" invariant.
        var lines = new List<(
            string AccountCode,
            MoneyEgp Debit,
            MoneyEgp Credit,
            string Description
        )>(4)
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
                    isReverseCharge
                        ? $"Purchase {invoice.DocumentNumber} — recoverable input VAT (reverse-charge self-account)"
                        : $"Purchase {invoice.DocumentNumber} — recoverable input VAT"
                )
            );
        }

        // P1.12 — reverse-charge self-accounting: credit Output VAT
        // by the same amount we just debited Input VAT. Net cash on
        // the period is zero, but both halves land in the VAT return
        // (regulator wants the flow declared, not netted away).
        if (isReverseCharge && reverseChargeOutputVatCredit > 0m)
        {
            lines.Add(
                (
                    ChartOfAccountCodes.OutputVatPayable,
                    MoneyEgp.Zero,
                    MoneyEgp.From(decimal.Round(reverseChargeOutputVatCredit, 2, MidpointRounding.ToEven)),
                    $"Purchase {invoice.DocumentNumber} — reverse-charge output VAT (self-declared)"
                )
            );
        }

        lines.Add(
            (
                ChartOfAccountCodes.AccountsPayable,
                MoneyEgp.Zero,
                MoneyEgp.From(decimal.Round(apCredit, 2, MidpointRounding.ToEven)),
                isReverseCharge
                    ? $"Purchase {invoice.DocumentNumber} — accrue payable (net only — VAT self-accounted)"
                    : $"Purchase {invoice.DocumentNumber} — accrue payable to supplier"
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
