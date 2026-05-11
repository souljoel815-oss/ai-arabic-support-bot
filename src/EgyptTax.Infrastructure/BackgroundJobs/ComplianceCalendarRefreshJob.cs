using EgyptTax.Application.Compliance.Calendar;
using EgyptTax.Domain.Compliance;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// P2.6 — daily Hangfire job that materialises the compliance
/// calendar for the current and next year. Idempotent under the
/// (kind, period_year, period_ordinal) unique index — a re-run only
/// inserts the rows the table doesn't already have.
///
/// Refreshes a 2-year window so the operator always sees at least
/// 12 months ahead. Past obligations stay in the table so the
/// dashboard can show overdue + recently-filed history.
/// </summary>
public sealed class ComplianceCalendarRefreshJob
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public ComplianceCalendarRefreshJob(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var company = await _db.Set<Company>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (company is null) return 0;

        var thisYear = _clock.UtcNow.Year;
        var existing = await _db.Set<ComplianceObligation>()
            .AsNoTracking()
            .Where(o => o.PeriodYear >= thisYear && o.PeriodYear <= thisYear + 1)
            .Select(o => new { o.Kind, o.PeriodYear, o.PeriodOrdinal })
            .ToListAsync(cancellationToken);
        var existingSet = existing
            .Select(e => (e.Kind, e.PeriodYear, e.PeriodOrdinal))
            .ToHashSet();

        var inserted = 0;
        foreach (var year in new[] { thisYear, thisYear + 1 })
        {
            foreach (var obligation in ComplianceObligationGenerator.ForYear(company, year))
            {
                var key = (obligation.Kind, obligation.PeriodYear, obligation.PeriodOrdinal);
                if (existingSet.Contains(key)) continue;
                _db.Add(obligation);
                inserted++;
            }
        }

        if (inserted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        return inserted;
    }
}
