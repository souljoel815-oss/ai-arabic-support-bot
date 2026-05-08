using EgyptTax.Application.Accounting;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;

namespace EgyptTax.Infrastructure.Accounting;

/// <summary>
/// Phase 9 / FR-052 / US7-ready — customer-receipt auto-emitter.
/// Per FR-052 the customer-WHT split is:
///
///   DR  Cash             net (= gross - wht)
///   DR  WhtReceivable    wht (omitted when zero)
///   CR  AR               gross
///
/// The WhtReceivable line is OMITTED entirely when
/// WhtReceivableAmount is zero (Phase 9 default — no customer
/// certificate recorded).
/// </summary>
public sealed class CustomerReceiptVoucherJournalEmitter : ICustomerReceiptVoucherJournalEmitter
{
    private readonly AppDbContext _db;

    public CustomerReceiptVoucherJournalEmitter(AppDbContext db)
    {
        _db = db;
    }

    public Task EmitForCustomerReceiptAsync(
        CustomerReceiptVoucher voucher,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        if (voucher.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for customer receipt voucher {voucher.Id}: state is {voucher.State}, not Posted.");
        }
        if (string.IsNullOrWhiteSpace(voucher.DocumentNumber))
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for customer receipt voucher {voucher.Id}: document number is empty.");
        }

        var lines = new List<(string AccountCode, MoneyEgp Debit, MoneyEgp Credit, string Description)>(3)
        {
            (ChartOfAccountCodes.Cash,
                voucher.NetCashReceived, MoneyEgp.Zero,
                $"Receipt {voucher.DocumentNumber} — cash leg"),
        };
        if (voucher.WhtReceivableAmount.Amount > 0m)
        {
            lines.Add((ChartOfAccountCodes.WhtReceivable,
                voucher.WhtReceivableAmount, MoneyEgp.Zero,
                $"Receipt {voucher.DocumentNumber} — customer-withheld tax"));
        }
        lines.Add((ChartOfAccountCodes.AccountsReceivable,
            MoneyEgp.Zero, voucher.GrossReceiptAmount,
            $"Receipt {voucher.DocumentNumber} — settle customer"));

        var entry = JournalEntry.Create(
            sourceDocumentId: voucher.Id,
            sourceDocumentNumber: voucher.DocumentNumber!,
            sourceDocumentType: DocumentType.CustomerReceiptVoucher,
            postedAtUtc: postedAtUtc,
            lines: lines);

        _db.Add(entry);
        return Task.CompletedTask;
    }
}
