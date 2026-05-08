using EgyptTax.Domain.Expenses;

namespace EgyptTax.Application.Accounting;

/// <summary>
/// US4 / FR-014 — port for the post-time journal emitter on the
/// expense surface. Expense is header-only (no per-line VAT split),
/// so the emitted journal is the simplest case: DR generic expense
/// account, CR accounts payable, both for the gross amount. The
/// FR-014 deductible flag does NOT change the journal — it drives
/// later tax-period reporting; the books move regardless.
/// </summary>
public interface IExpenseJournalEmitter
{
    Task EmitForExpenseAsync(
        Expense expense,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    );
}
