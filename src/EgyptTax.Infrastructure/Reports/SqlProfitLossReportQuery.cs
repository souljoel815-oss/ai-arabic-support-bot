using EgyptTax.Application.Reports;
using EgyptTax.Domain.Accounting;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Reports;

/// <summary>
/// EF-backed Profit &amp; Loss / Income Statement. Same data source as the
/// trial balance — every <c>JournalEntry</c> + <c>JournalEntryLine</c>
/// posted within the period — filtered to accounts that start with the
/// revenue (4) or expense (5) prefix per the Egyptian SME chart of
/// accounts (see <see cref="ChartOfAccountCodes"/>).
///
/// SQLite-friendly: projects to a flat in-memory list before grouping
/// because SQLite's EF translator can't <c>Sum()</c> decimals server-side.
/// Volume = one period of posted journal lines (small for an SMB).
/// </summary>
public sealed class SqlProfitLossReportQuery : IProfitLossReportQuery
{
    private readonly AppDbContext _db;

    public SqlProfitLossReportQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProfitLossReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default
    )
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException(
                $"PeriodEnd ({periodEnd:yyyy-MM-dd}) cannot be before PeriodStart ({periodStart:yyyy-MM-dd}).",
                nameof(periodEnd)
            );
        }

        var startUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var endExclusive = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Pull everything in the period then filter + group in memory.
        // SQLite's EF translator can't Sum() decimals server-side; we
        // also dodge a CA1866 vs EF-translation conflict by doing the
        // 4xxx/5xxx prefix filter post-hydration with the char-overload
        // StartsWith.
        var allLinesInPeriod = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc >= startUtc && e.PostedAtUtc < endExclusive
            select new
            {
                l.AccountCode,
                Debit = l.Debit.Amount,
                Credit = l.Credit.Amount,
            }
        ).ToListAsync(cancellationToken);

        var rawLines = allLinesInPeriod
            .Where(l => l.AccountCode.StartsWith('4') || l.AccountCode.StartsWith('5'))
            .ToList();

        var grouped = rawLines
            .GroupBy(x => x.AccountCode)
            .Select(g =>
            {
                var totalDebit = g.Sum(x => x.Debit);
                var totalCredit = g.Sum(x => x.Credit);
                // Revenue accounts (4xxx): natural credit balance, so
                //   net contribution = credit − debit.
                // Expense accounts (5xxx): natural debit balance, so
                //   net contribution = debit − credit.
                var isRevenue = g.Key.StartsWith('4');
                var amount = isRevenue
                    ? totalCredit - totalDebit
                    : totalDebit - totalCredit;
                return new
                {
                    AccountCode = g.Key,
                    IsRevenue = isRevenue,
                    Amount = decimal.Round(amount, 2, MidpointRounding.ToEven),
                };
            })
            // Drop accounts with zero net activity — they would clutter
            // the report without adding information.
            .Where(r => r.Amount != 0m)
            .ToList();

        var revenueRows = grouped
            .Where(r => r.IsRevenue)
            .OrderBy(r => r.AccountCode, StringComparer.Ordinal)
            .Select(r => new ProfitLossRow(r.AccountCode, MoneyEgp.From(r.Amount)))
            .ToList();

        var expenseRows = grouped
            .Where(r => !r.IsRevenue)
            .OrderBy(r => r.AccountCode, StringComparer.Ordinal)
            .Select(r => new ProfitLossRow(r.AccountCode, MoneyEgp.From(r.Amount)))
            .ToList();

        var totalRevenue = revenueRows.Sum(r => r.Amount.Amount);
        var totalExpenses = expenseRows.Sum(r => r.Amount.Amount);
        var netIncome = totalRevenue - totalExpenses;

        return new ProfitLossReport(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            TotalRevenue: MoneyEgp.From(decimal.Round(totalRevenue, 2, MidpointRounding.ToEven)),
            TotalExpenses: MoneyEgp.From(decimal.Round(totalExpenses, 2, MidpointRounding.ToEven)),
            NetIncome: MoneyEgp.From(decimal.Round(netIncome, 2, MidpointRounding.ToEven)),
            RevenueRows: revenueRows,
            ExpenseRows: expenseRows
        );
    }
}
