using EgyptTax.Application.Accounting;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;

namespace EgyptTax.Infrastructure.Accounting;

/// <summary>
/// US4 / FR-014 — expense auto-emitter. The simplest case in the
/// auto-emit family: 2-row JE, DR GenericExpense for the full
/// amount, CR AccountsPayable for the same amount. No VAT split
/// because the Expense aggregate is header-only (no per-line VAT
/// columns). The deductible flag affects FR-021 taxable-income
/// reporting but not the journal — the books move equally for
/// deductible and non-deductible expenses, the difference shows up
/// when the period close re-classifies the deductible portion.
///
/// Without this emitter the trial balance was silently missing the
/// expense line; this closes the gap so US9 inspector bundles
/// reflect every posted document.
/// </summary>
public sealed class ExpenseJournalEmitter : IExpenseJournalEmitter
{
    private readonly AppDbContext _db;

    public ExpenseJournalEmitter(AppDbContext db)
    {
        _db = db;
    }

    public Task EmitForExpenseAsync(
        Expense expense,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(expense);
        if (expense.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for expense {expense.Id}: state is {expense.State}, not Posted."
            );
        }
        if (string.IsNullOrWhiteSpace(expense.DocumentNumber))
        {
            throw new InvalidOperationException(
                $"Cannot emit journal for expense {expense.Id}: document number is empty."
            );
        }

        var amount = MoneyEgp.From(
            decimal.Round(expense.Amount.Amount, 2, MidpointRounding.ToEven)
        );
        var entry = JournalEntry.Create(
            sourceDocumentId: expense.Id,
            sourceDocumentNumber: expense.DocumentNumber!,
            sourceDocumentType: DocumentType.Expense,
            postedAtUtc: postedAtUtc,
            lines: new[]
            {
                (
                    ChartOfAccountCodes.GenericExpense,
                    amount,
                    MoneyEgp.Zero,
                    $"Expense {expense.DocumentNumber} — book expense"
                ),
                (
                    ChartOfAccountCodes.AccountsPayable,
                    MoneyEgp.Zero,
                    amount,
                    $"Expense {expense.DocumentNumber} — accrue payable"
                ),
            }
        );

        _db.Add(entry);
        return Task.CompletedTask;
    }
}
