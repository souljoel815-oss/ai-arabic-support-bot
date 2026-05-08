using EgyptTax.Application.Wht;
using EgyptTax.Domain.Tax;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Wht;

/// <summary>
/// FR-047 / T211 — EF-backed dashboard projection. Three queries
/// run independently so a slow filings page doesn't drag the
/// owed/expected sums; in-memory roll-up applies the overdue
/// derivation + penalty estimator since the math is cleaner in
/// C# than in SQL.
///
/// Egyptian Form 41 due-date model used here: <strong>30 days
/// after the end of the quarter</strong> (the operator usually
/// has the month following the quarter close to file). The
/// penalty estimator is a placeholder until the official ETA
/// schedule is wired — see <see cref="EstimatedPenalty"/>.
/// </summary>
public sealed class SqlWhtLifecycleDashboardQuery : IWhtLifecycleDashboardQuery
{
    /// <summary>FR-047 — number of days after a quarter ends
    /// during which a Form 41 can be filed without penalty.</summary>
    public const int Form41FilingGraceDays = 30;

    private readonly AppDbContext _db;

    public SqlWhtLifecycleDashboardQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WhtLifecycleDashboard> GetAsync(
        DateOnly asOf,
        CancellationToken cancellationToken = default
    )
    {
        // Owed view: outbound certs not yet stamped into a filing.
        var unfiledOutbound = await _db.Set<WhtCertificate>()
            .AsNoTracking()
            .Where(c =>
                c.Direction == WhtCertificateDirection.OutboundToSupplier
                && c.IncludedInForm41FilingId == null
            )
            .Select(c => new { c.Date, Amount = c.AmountWithheld.Amount })
            .ToListAsync(cancellationToken);

        var owed = new WhtOwedView(
            TotalAccruedNotYetFiled: unfiledOutbound.Sum(x => x.Amount),
            CertCount: unfiledOutbound.Count,
            OldestUnfiledCertDate: unfiledOutbound.Count == 0
                ? null
                : unfiledOutbound.Min(x => x.Date)
        );

        // Expected view: inbound certs (customer-issued).
        var inbound = await _db.Set<WhtCertificate>()
            .AsNoTracking()
            .Where(c => c.Direction == WhtCertificateDirection.InboundFromCustomer)
            .Select(c => c.AmountWithheld.Amount)
            .ToListAsync(cancellationToken);
        var expected = new WhtExpectedView(
            TotalReceivableFromCustomerWht: inbound.Sum(),
            InboundCertCount: inbound.Count
        );

        // Filings: every row, decorated with overdue derivation +
        // penalty estimate.
        var filings = await _db.Set<Form41Filing>()
            .AsNoTracking()
            .OrderByDescending(f => f.FiscalYear)
            .ThenByDescending(f => f.Quarter)
            .ToListAsync(cancellationToken);

        var filingRows = filings
            .Select(f =>
            {
                var dueDate = QuarterEndDate(f.FiscalYear, f.Quarter)
                    .AddDays(Form41FilingGraceDays);
                var derivedStatus = DeriveStatus(f, asOf, dueDate);
                var daysOverdue =
                    derivedStatus == Form41Status.Overdue
                        ? Math.Max(0, asOf.DayNumber - dueDate.DayNumber)
                        : 0;
                return new Form41FilingRow(
                    Id: f.Id,
                    FiscalYear: f.FiscalYear,
                    Quarter: f.Quarter,
                    DerivedStatus: derivedStatus,
                    DueDate: dueDate,
                    DaysOverdue: daysOverdue,
                    TotalWhtPayable: f.TotalWhtPayable.Amount,
                    LineCount: f.LineCount,
                    EstimatedPenalty: derivedStatus == Form41Status.Overdue
                        ? EstimatedPenalty(f.TotalWhtPayable.Amount, daysOverdue)
                        : null
                );
            })
            .ToList();

        return new WhtLifecycleDashboard(owed, expected, filingRows);
    }

    /// <summary>FR-047 — placeholder penalty model until the
    /// official ETA schedule is wired: 1% of the WHT payable per
    /// month overdue, capped at 25% of the total. Intentionally
    /// simple; a proper rule engine slots in here later. The
    /// floor of 1 month means even a 1-day overdue filing carries
    /// the first-month penalty (matches the typical Egyptian
    /// practice of charging on entry into the late period).</summary>
    private static decimal EstimatedPenalty(decimal totalWhtPayable, int daysOverdue)
    {
        var monthsOverdue = Math.Max(1, (int)Math.Ceiling(daysOverdue / 30.0));
        var rate = Math.Min(0.25m, monthsOverdue * 0.01m);
        return decimal.Round(totalWhtPayable * rate, 2, MidpointRounding.ToEven);
    }

    private static Form41Status DeriveStatus(Form41Filing f, DateOnly asOf, DateOnly dueDate)
    {
        if (f.Status == Form41Status.Filed)
            return Form41Status.Filed;
        if (asOf > dueDate)
            return Form41Status.Overdue;
        return Form41Status.Unfiled;
    }

    private static DateOnly QuarterEndDate(int fiscalYear, int quarter)
    {
        var endMonth = quarter * 3;
        return new DateOnly(fiscalYear, endMonth, DateTime.DaysInMonth(fiscalYear, endMonth));
    }
}
