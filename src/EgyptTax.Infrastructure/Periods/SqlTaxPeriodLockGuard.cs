using EgyptTax.Application.Periods;
using EgyptTax.Domain.Periods;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Periods;

/// <summary>
/// FR-037 — EF-backed implementation. Single seek against
/// <c>ux_tax_periods_kind_year_month</c>. If no row exists for the
/// (year, month) the period is considered Open by default — the
/// lock-on-first-use semantics mean an unlocked period costs zero
/// rows in the table.
/// </summary>
public sealed class SqlTaxPeriodLockGuard : ITaxPeriodLockGuard
{
    private readonly AppDbContext _db;

    public SqlTaxPeriodLockGuard(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TaxPeriodLockCheck> CheckVatMonthAsync(
        DateOnly documentDate,
        CancellationToken cancellationToken = default)
    {
        var year = documentDate.Year;
        var month = documentDate.Month;

        var row = await _db.Set<TaxPeriod>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.PeriodKind == TaxPeriodKind.VatMonth
                && p.Year == year
                && p.MonthOrQuarter == month, cancellationToken);

        if (row is null || row.Status == TaxPeriodStatus.Open)
        {
            return new TaxPeriodLockCheck(
                IsLocked: false, Year: year, MonthOrQuarter: month,
                LockedAtUtc: null, LockedByUserId: null, LockedReason: null);
        }

        return new TaxPeriodLockCheck(
            IsLocked: true,
            Year: year,
            MonthOrQuarter: month,
            LockedAtUtc: row.LockedAtUtc,
            LockedByUserId: row.LockedByUserId,
            LockedReason: row.LockedReason);
    }
}
