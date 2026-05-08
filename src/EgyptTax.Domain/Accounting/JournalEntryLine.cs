using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Accounting;

/// <summary>
/// T095 — a single side of a balanced journal entry. By double-entry
/// convention exactly one of <see cref="Debit"/> / <see cref="Credit"/>
/// MUST be non-zero on a given row, and both values are non-negative
/// (signs are conveyed by the debit-vs-credit choice rather than by
/// negating the amount). The <see cref="Description"/> is a short
/// human label so the GL printout reads like an accountant's book.
/// </summary>
public sealed class JournalEntryLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JournalEntryId { get; init; }
    public string AccountCode { get; init; } = "";
    public MoneyEgp Debit { get; init; } = MoneyEgp.Zero;
    public MoneyEgp Credit { get; init; } = MoneyEgp.Zero;
    public string Description { get; init; } = "";

    private JournalEntryLine() { }

    public JournalEntryLine(
        Guid journalEntryId,
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
                "A journal line MUST debit XOR credit — not both. Split into two rows if both sides are needed."
            );
        }
        if (debit.Amount == 0m && credit.Amount == 0m)
        {
            throw new ArgumentException(
                "A journal line MUST be either a debit or a credit; both being zero is meaningless."
            );
        }

        JournalEntryId = journalEntryId;
        AccountCode = accountCode;
        Debit = debit;
        Credit = credit;
        Description = description ?? "";
    }
}
