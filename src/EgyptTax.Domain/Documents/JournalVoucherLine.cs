using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Documents;

/// <summary>
/// One side of a balanced <see cref="JournalVoucher"/>. Mirrors the
/// existing JournalEntryLine invariants: exactly one of debit/credit
/// must be non-zero, both values non-negative (signs conveyed by
/// the side, not the magnitude). Account is referenced by string
/// code (matches the chart-of-account constants in
/// <c>ChartOfAccountCodes</c>; full FK lands when the chart-of-
/// accounts table itself is built out in US5).
/// </summary>
public sealed class JournalVoucherLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JournalVoucherId { get; init; }
    public string AccountCode { get; init; } = "";
    public MoneyEgp Debit { get; init; } = MoneyEgp.Zero;
    public MoneyEgp Credit { get; init; } = MoneyEgp.Zero;
    public string Description { get; init; } = "";

    private JournalVoucherLine() { }

    public JournalVoucherLine(
        Guid journalVoucherId,
        string accountCode,
        MoneyEgp debit,
        MoneyEgp credit,
        string description
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountCode);
        if (debit.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(debit),
                "Debit amount cannot be negative."
            );
        }
        if (credit.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(credit),
                "Credit amount cannot be negative."
            );
        }
        if (debit.Amount > 0m && credit.Amount > 0m)
        {
            throw new ArgumentException(
                "A journal voucher line MUST debit XOR credit — not both. Split into two rows if both sides are needed."
            );
        }
        if (debit.Amount == 0m && credit.Amount == 0m)
        {
            throw new ArgumentException(
                "A journal voucher line MUST be either a debit or a credit; both being zero is meaningless."
            );
        }

        JournalVoucherId = journalVoucherId;
        AccountCode = accountCode;
        Debit = debit;
        Credit = credit;
        Description = description ?? "";
    }
}
