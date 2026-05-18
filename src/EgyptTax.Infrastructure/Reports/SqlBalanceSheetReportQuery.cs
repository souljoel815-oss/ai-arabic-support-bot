using EgyptTax.Application.Reports;
using EgyptTax.Domain.Accounting;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Reports;

/// <summary>
/// EF-backed Balance Sheet. Walks every journal-entry line posted on
/// or before <c>AsOfDate</c>, groups by account, and classifies by
/// chart-of-account leading digit (1 = Assets, 2 = Liabilities,
/// 3 = Equity, 4/5 = Revenue/Expense collapsed into Retained Earnings).
///
/// Sign conventions:
///   Asset balance       = sum(debit) - sum(credit)   (debit-natural)
///   Liability balance   = sum(credit) - sum(debit)   (credit-natural)
///   Equity balance      = sum(credit) - sum(debit)   (credit-natural)
///   Retained earnings   = (revenue credits - debits) - (expense debits - credits)
///
/// SQLite-friendly: projects to a flat in-memory list before grouping
/// because SQLite's EF translator can't <c>Sum()</c> decimals server-side.
/// </summary>
public sealed class SqlBalanceSheetReportQuery : IBalanceSheetReportQuery
{
    private readonly AppDbContext _db;

    public SqlBalanceSheetReportQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BalanceSheetReport> RunAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default
    )
    {
        // Cumulative — everything posted strictly before the day AFTER asOfDate.
        var endExclusive = asOfDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var rawLines = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc < endExclusive
            select new
            {
                l.AccountCode,
                Debit = l.Debit.Amount,
                Credit = l.Credit.Amount,
            }
        ).ToListAsync(cancellationToken);

        var grouped = rawLines
            .GroupBy(x => x.AccountCode)
            .Select(g =>
            {
                var totalDebit = g.Sum(x => x.Debit);
                var totalCredit = g.Sum(x => x.Credit);
                return new
                {
                    AccountCode = g.Key,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                };
            })
            .ToList();

        var assetRows = grouped
            .Where(g => g.AccountCode.StartsWith('1'))
            .Select(g => new
            {
                g.AccountCode,
                Balance = decimal.Round(g.TotalDebit - g.TotalCredit, 2, MidpointRounding.ToEven),
            })
            .Where(r => r.Balance != 0m)
            .OrderBy(r => r.AccountCode, StringComparer.Ordinal)
            .Select(r => new BalanceSheetRow(r.AccountCode, MoneyEgp.From(r.Balance)))
            .ToList();

        var liabilityRows = grouped
            .Where(g => g.AccountCode.StartsWith('2'))
            .Select(g => new
            {
                g.AccountCode,
                Balance = decimal.Round(g.TotalCredit - g.TotalDebit, 2, MidpointRounding.ToEven),
            })
            .Where(r => r.Balance != 0m)
            .OrderBy(r => r.AccountCode, StringComparer.Ordinal)
            .Select(r => new BalanceSheetRow(r.AccountCode, MoneyEgp.From(r.Balance)))
            .ToList();

        var equityRows = grouped
            .Where(g => g.AccountCode.StartsWith('3'))
            .Select(g => new
            {
                g.AccountCode,
                Balance = decimal.Round(g.TotalCredit - g.TotalDebit, 2, MidpointRounding.ToEven),
            })
            .Where(r => r.Balance != 0m)
            .OrderBy(r => r.AccountCode, StringComparer.Ordinal)
            .Select(r => new BalanceSheetRow(r.AccountCode, MoneyEgp.From(r.Balance)))
            .ToList();

        // Retained earnings = cumulative net income (revenue - expense).
        var revenueNet = grouped
            .Where(g => g.AccountCode.StartsWith('4'))
            .Sum(g => g.TotalCredit - g.TotalDebit);
        var expenseNet = grouped
            .Where(g => g.AccountCode.StartsWith('5'))
            .Sum(g => g.TotalDebit - g.TotalCredit);
        var retainedEarnings = decimal.Round(revenueNet - expenseNet, 2, MidpointRounding.ToEven);

        var totalAssets = assetRows.Sum(r => r.Balance.Amount);
        var totalLiabilities = liabilityRows.Sum(r => r.Balance.Amount);
        var totalEquity = equityRows.Sum(r => r.Balance.Amount);

        // The accounting equation: A = L + E + RE.
        var rhs = decimal.Round(totalLiabilities + totalEquity + retainedEarnings, 2, MidpointRounding.ToEven);
        var lhs = decimal.Round(totalAssets, 2, MidpointRounding.ToEven);
        var isBalanced = lhs == rhs;

        return new BalanceSheetReport(
            AsOfDate: asOfDate,
            TotalAssets: MoneyEgp.From(lhs),
            TotalLiabilities: MoneyEgp.From(decimal.Round(totalLiabilities, 2, MidpointRounding.ToEven)),
            TotalEquity: MoneyEgp.From(decimal.Round(totalEquity, 2, MidpointRounding.ToEven)),
            RetainedEarnings: MoneyEgp.From(retainedEarnings),
            IsBalanced: isBalanced,
            AssetRows: assetRows,
            LiabilityRows: liabilityRows,
            EquityRows: equityRows
        );
    }
}
