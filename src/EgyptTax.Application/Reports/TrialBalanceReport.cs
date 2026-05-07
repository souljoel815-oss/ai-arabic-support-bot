using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Reports;

/// <summary>
/// FR-024 — trial balance. Sums debits + credits from
/// <c>JournalEntryLine</c> rows scoped to journal entries posted
/// within the period, grouped by chart-of-account code. The
/// bedrock invariant <c>SUM(debits) == SUM(credits)</c> is
/// surfaced as <see cref="IsBalanced"/>; if it ever flips false
/// the GL has been corrupted (the journal-entry factory enforces
/// the invariant at write time, so a false value here means
/// out-of-band tampering).
/// </summary>
public sealed record TrialBalanceReport(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    MoneyEgp TotalDebits,
    MoneyEgp TotalCredits,
    bool IsBalanced,
    IReadOnlyList<TrialBalanceRow> Rows);

/// <summary>One per account code with non-zero activity in the period.</summary>
public sealed record TrialBalanceRow(
    string AccountCode,
    MoneyEgp TotalDebit,
    MoneyEgp TotalCredit,
    MoneyEgp NetBalance);
