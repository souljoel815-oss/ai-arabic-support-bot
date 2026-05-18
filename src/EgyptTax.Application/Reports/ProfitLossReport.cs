using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Reports;

/// <summary>
/// Income Statement / قائمة الدخل. Period-scoped P&amp;L.
///
/// Computation: walks every journal-entry line posted within
/// [PeriodStart, PeriodEnd], filters to revenue + expense accounts
/// (chart-of-account leading digit 4 = revenue, 5 = expense per
/// <see cref="EgyptTax.Domain.Accounting.ChartOfAccountCodes"/> convention),
/// and aggregates by account.
///
/// Revenue contribution per row = TotalCredit − TotalDebit
///   (revenues normally have a credit balance; debits are reversals).
/// Expense contribution per row = TotalDebit − TotalCredit
///   (expenses normally have a debit balance; credits are reversals).
///
/// <see cref="NetIncome"/> = <see cref="TotalRevenue"/> − <see cref="TotalExpenses"/>.
/// </summary>
public sealed record ProfitLossReport(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    MoneyEgp TotalRevenue,
    MoneyEgp TotalExpenses,
    MoneyEgp NetIncome,
    IReadOnlyList<ProfitLossRow> RevenueRows,
    IReadOnlyList<ProfitLossRow> ExpenseRows
);

/// <summary>One per account code with non-zero activity in the period.</summary>
public sealed record ProfitLossRow(
    string AccountCode,
    MoneyEgp Amount
);
