using EgyptTax.Application.Reports;
using EgyptTax.Domain.Accounting;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Reports;

/// <summary>
/// FR-024 — EF-backed trial balance. Selects every journal-entry
/// line whose parent entry was posted within the period (using
/// <c>JournalEntry.PostedAtUtc</c> projected to the parent's date),
/// groups by <c>AccountCode</c>, sums debits + credits.
///
/// Period boundary uses the journal entry's posting date in the
/// parent SalesInvoice / PurchaseInvoice's date — the test
/// inclusivity matches the other two reports (PeriodStart and
/// PeriodEnd both included).
/// </summary>
public sealed class SqlTrialBalanceReportQuery : ITrialBalanceReportQuery
{
    private readonly AppDbContext _db;

    public SqlTrialBalanceReportQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TrialBalanceReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException(
                $"PeriodEnd ({periodEnd:yyyy-MM-dd}) cannot be before PeriodStart ({periodStart:yyyy-MM-dd}).",
                nameof(periodEnd));
        }

        // Convert the inclusive date window to a UTC datetime range.
        // Journal entries carry PostedAtUtc which is a wall-clock
        // moment; the period filter spans the entire end-day.
        var startUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var endExclusive = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var rows = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc >= startUtc && e.PostedAtUtc < endExclusive
            group l by l.AccountCode into g
            select new
            {
                AccountCode = g.Key,
                TotalDebit = g.Sum(x => x.Debit.Amount),
                TotalCredit = g.Sum(x => x.Credit.Amount),
            }).ToListAsync(cancellationToken);

        var ordered = rows
            .OrderBy(r => r.AccountCode, StringComparer.Ordinal)
            .Select(r => new TrialBalanceRow(
                AccountCode: r.AccountCode,
                TotalDebit: MoneyEgp.From(decimal.Round(r.TotalDebit, 2, MidpointRounding.ToEven)),
                TotalCredit: MoneyEgp.From(decimal.Round(r.TotalCredit, 2, MidpointRounding.ToEven)),
                NetBalance: MoneyEgp.From(decimal.Round(r.TotalDebit - r.TotalCredit, 2, MidpointRounding.ToEven))))
            .ToList();

        var totalDebits = decimal.Round(rows.Sum(r => r.TotalDebit), 2, MidpointRounding.ToEven);
        var totalCredits = decimal.Round(rows.Sum(r => r.TotalCredit), 2, MidpointRounding.ToEven);

        return new TrialBalanceReport(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            TotalDebits: MoneyEgp.From(totalDebits),
            TotalCredits: MoneyEgp.From(totalCredits),
            IsBalanced: totalDebits == totalCredits,
            Rows: ordered);
    }
}
