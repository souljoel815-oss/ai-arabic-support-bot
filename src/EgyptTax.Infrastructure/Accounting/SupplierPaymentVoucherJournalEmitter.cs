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
/// Phase 9 / FR-051 / US7-ready — supplier-payment auto-emitter.
/// One balanced JournalEntry per posted voucher. Per FR-051 the
/// supplier-side WHT split is:
///
///   DR  AP                gross
///   CR  Cash              net (= gross - wht)
///   CR  WhtPayable        wht (omitted when zero)
///
/// The WhtPayable line is OMITTED entirely when WhtPayableAmount
/// is zero (Phase 9 default — no WHT compute yet) — a zero-amount
/// line would violate the JournalEntryLine "debit XOR credit > 0"
/// invariant.
/// </summary>
public sealed class SupplierPaymentVoucherJournalEmitter : ISupplierPaymentVoucherJournalEmitter
{
    private readonly AppDbContext _db;

    public SupplierPaymentVoucherJournalEmitter(AppDbContext db)
    {
        _db = db;
    }

    public async Task EmitForSupplierPaymentAsync(
        SupplierPaymentVoucher voucher,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(voucher);
        if (voucher.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for supplier payment voucher {voucher.Id}: state is {voucher.State}, not Posted."
            );
        }
        if (string.IsNullOrWhiteSpace(voucher.DocumentNumber))
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for supplier payment voucher {voucher.Id}: document number is empty."
            );
        }

        // P3.1 — multi-cashbox/bank: when the voucher has been pinned
        // to a specific CashAccount, credit that account's code (e.g.
        // "1100.02 — CIB EGP") instead of the legacy hard-coded
        // "1100 Cash" so the trial balance and per-account drill-
        // down show the right balance per cashbox.
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
        )>(3)
        {
            (
                ChartOfAccountCodes.AccountsPayable,
                voucher.GrossPaymentAmount,
                MoneyEgp.Zero,
                $"Payment {voucher.DocumentNumber} — settle supplier"
            ),
            (
                cashAccountCode,
                MoneyEgp.Zero,
                voucher.NetCashPaid,
                $"Payment {voucher.DocumentNumber} — cash leg"
            ),
        };
        if (voucher.WhtPayableAmount.Amount > 0m)
        {
            lines.Add(
                (
                    ChartOfAccountCodes.WhtPayable,
                    MoneyEgp.Zero,
                    voucher.WhtPayableAmount,
                    $"Payment {voucher.DocumentNumber} — WHT withheld from supplier"
                )
            );
        }

        var entry = JournalEntry.Create(
            sourceDocumentId: voucher.Id,
            sourceDocumentNumber: voucher.DocumentNumber!,
            sourceDocumentType: DocumentType.SupplierPaymentVoucher,
            postedAtUtc: postedAtUtc,
            lines: lines
        );

        _db.Add(entry);
    }
}
