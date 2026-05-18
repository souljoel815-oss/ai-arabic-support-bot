using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Reports;

/// <summary>
/// Balance Sheet / الميزانية. Point-in-time snapshot computed as the
/// cumulative effect of every journal-entry line posted on or before
/// <see cref="AsOfDate"/>, classified by chart-of-account leading digit:
/// 1 = Assets, 2 = Liabilities, 3 = Equity (per
/// <see cref="EgyptTax.Domain.Accounting.ChartOfAccountCodes"/>).
///
/// Retained Earnings is the cumulative net income (revenue 4xxx minus
/// expense 5xxx) from beginning of time to <see cref="AsOfDate"/>. The
/// fundamental accounting equation
///   Assets = Liabilities + Equity + Retained Earnings
/// must hold; any imbalance signals out-of-band tampering with the
/// underlying journal tables (the journal-entry factory enforces the
/// debit==credit invariant at write time).
/// </summary>
public sealed record BalanceSheetReport(
    DateOnly AsOfDate,
    MoneyEgp TotalAssets,
    MoneyEgp TotalLiabilities,
    MoneyEgp TotalEquity,
    MoneyEgp RetainedEarnings,
    bool IsBalanced,
    IReadOnlyList<BalanceSheetRow> AssetRows,
    IReadOnlyList<BalanceSheetRow> LiabilityRows,
    IReadOnlyList<BalanceSheetRow> EquityRows
);

/// <summary>One per account with non-zero cumulative balance as of the snapshot date.</summary>
public sealed record BalanceSheetRow(
    string AccountCode,
    MoneyEgp Balance
);
