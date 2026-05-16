using EgyptTax.Application.Accounting;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Accounting;

/// <summary>
/// Phase 9 / FR-052 / US7-ready — customer-receipt auto-emitter.
/// v5 E.8 adds the optional discount leg:
///
///   DR  Cash             net (= gross - wht - discount)
///   DR  WhtReceivable    wht       (omitted when zero)
///   DR  SalesDiscountTaken discount (omitted when zero)
///   CR  AR               gross
///
/// The WhtReceivable + SalesDiscountTaken lines are OMITTED when
/// their respective amounts are zero (default: no certificate, no
/// discount → 2-line DR Cash / CR AR).
/// </summary>
public sealed class CustomerReceiptVoucherJournalEmitter : ICustomerReceiptVoucherJournalEmitter
{
    private readonly AppDbContext _db;

    public CustomerReceiptVoucherJournalEmitter(AppDbContext db)
    {
        _db = db;
    }

    public async Task EmitForCustomerReceiptAsync(
        CustomerReceiptVoucher voucher,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(voucher);
        if (voucher.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for customer receipt voucher {voucher.Id}: state is {voucher.State}, not Posted."
            );
        }
        if (string.IsNullOrWhiteSpace(voucher.DocumentNumber))
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for customer receipt voucher {voucher.Id}: document number is empty."
            );
        }

        // P3.1 — multi-cashbox/bank: when the voucher has been
        // pinned to a specific CashAccount, debit that account's
        // code instead of the legacy hard-coded "1100 Cash" so the
        // trial balance shows the right balance per cashbox.
        var cashAccountCode = ChartOfAccountCodes.Cash;
        if (voucher.CashAccountId is { } cashId)
        {
            cashAccountCode = await _db.Set<CashAccount>()
                .AsNoTracking()
                .Where(a => a.Id == cashId)
                .Select(a => a.AccountCode)
                .FirstOrDefaultAsync(cancellationToken)
                ?? ChartOfAccountCodes.Cash;
        }

        var lines = new List<(
            string AccountCode,
            MoneyEgp Debit,
            MoneyEgp Credit,
            string Description
        )>(4)
        {
            (
                cashAccountCode,
                voucher.NetCashReceived,
                MoneyEgp.Zero,
                $"Receipt {voucher.DocumentNumber} — cash leg"
            ),
        };
        if (voucher.WhtReceivableAmount.Amount > 0m)
        {
            lines.Add(
                (
                    ChartOfAccountCodes.WhtReceivable,
                    voucher.WhtReceivableAmount,
                    MoneyEgp.Zero,
                    $"Receipt {voucher.DocumentNumber} — customer-withheld tax"
                )
            );
        }
        if (voucher.DiscountTakenAmount.Amount > 0m)
        {
            lines.Add(
                (
                    ChartOfAccountCodes.SalesDiscountTaken,
                    voucher.DiscountTakenAmount,
                    MoneyEgp.Zero,
                    $"Receipt {voucher.DocumentNumber} — early-payment discount"
                )
            );
        }
        lines.Add(
            (
                ChartOfAccountCodes.AccountsReceivable,
                MoneyEgp.Zero,
                voucher.GrossReceiptAmount,
                $"Receipt {voucher.DocumentNumber} — settle customer"
            )
        );

        var entry = JournalEntry.Create(
            sourceDocumentId: voucher.Id,
            sourceDocumentNumber: voucher.DocumentNumber!,
            sourceDocumentType: DocumentType.CustomerReceiptVoucher,
            postedAtUtc: postedAtUtc,
            lines: lines
        );

        _db.Add(entry);
    }
}
