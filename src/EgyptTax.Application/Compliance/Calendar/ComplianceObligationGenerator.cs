using EgyptTax.Domain.Compliance;
using EgyptTax.Domain.MasterData;

namespace EgyptTax.Application.Compliance.Calendar;

/// <summary>
/// P2.6 — derives the statutory filing calendar for a company over a
/// year window. The set of obligations depends on the company's
/// regime (Standard vs Law 6 simplified) and fiscal-year start.
///
/// Egyptian filing calendar baked in here:
///   - VAT-monthly (Standard): due 28th of the following month.
///   - VAT-quarterly (Law 6 simplified): due 28th of the month after
///     each fiscal quarter ends.
///   - Form 41 (WHT, both regimes): due last day of the month after
///     each calendar quarter.
///   - Income tax annual (Standard): due 4 months after FY end.
///   - Turnover tax annual (Law 6 simplified): due 4 months after
///     FY end.
///
/// Pure function — caller decides what to do with the produced list
/// (typically: insert any (kind, year, ordinal) not already in the
/// table; idempotent under the unique index from
/// <c>ComplianceObligationConfiguration</c>).
/// </summary>
public static class ComplianceObligationGenerator
{
    /// <summary>
    /// Generate every obligation for the given calendar year.
    /// Returns them sorted ascending by DueDate so callers can show
    /// a chronological calendar without re-sorting.
    /// </summary>
    public static IReadOnlyList<ComplianceObligation> ForYear(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        var result = new List<ComplianceObligation>(20);

        if (company.TaxRegime == TaxRegime.Standard)
        {
            // VAT-monthly: 12 returns per year, due 28th of the
            // following month.
            for (var m = 1; m <= 12; m++)
            {
                var periodStart = new DateOnly(year, m, 1);
                var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                var due = AddMonths(periodStart, 1);
                due = new DateOnly(due.Year, due.Month, 28);
                result.Add(new ComplianceObligation(
                    ComplianceObligationKind.VatMonthly,
                    periodYear: year,
                    periodOrdinal: m,
                    periodStart: periodStart,
                    periodEnd: periodEnd,
                    dueDate: due));
            }
        }
        else
        {
            // VAT-quarterly: 4 returns per year, due 28th of the month
            // after each quarter ends.
            for (var q = 1; q <= 4; q++)
            {
                var startMonth = (q - 1) * 3 + 1;
                var periodStart = new DateOnly(year, startMonth, 1);
                var periodEnd = periodStart.AddMonths(3).AddDays(-1);
                var firstAfter = periodEnd.AddDays(1);
                var due = new DateOnly(firstAfter.Year, firstAfter.Month, 28);
                result.Add(new ComplianceObligation(
                    ComplianceObligationKind.VatQuarterly,
                    periodYear: year,
                    periodOrdinal: q,
                    periodStart: periodStart,
                    periodEnd: periodEnd,
                    dueDate: due));
            }
        }

        // Form 41 quarterly (both regimes — WHT obligation is
        // independent of VAT regime). Due last day of the month
        // after each calendar quarter.
        for (var q = 1; q <= 4; q++)
        {
            var startMonth = (q - 1) * 3 + 1;
            var periodStart = new DateOnly(year, startMonth, 1);
            var periodEnd = periodStart.AddMonths(3).AddDays(-1);
            var dueMonth = periodEnd.AddDays(1);
            var due = LastDayOfMonth(dueMonth.Year, dueMonth.Month);
            result.Add(new ComplianceObligation(
                ComplianceObligationKind.Form41Quarterly,
                periodYear: year,
                periodOrdinal: q,
                periodStart: periodStart,
                periodEnd: periodEnd,
                dueDate: due));
        }

        // Annual income / turnover tax. FY ends one month before the
        // company's FY-start month; the annual return is due 4 months
        // after FY end.
        var fyEndMonth = company.FiscalYearStartMonth == 1
            ? 12
            : company.FiscalYearStartMonth - 1;
        var fyStart = new DateOnly(year, company.FiscalYearStartMonth, 1);
        var fyEnd = company.FiscalYearStartMonth == 1
            ? new DateOnly(year, 12, 31)
            : LastDayOfMonth(year - (company.FiscalYearStartMonth > 1 ? 0 : 0), fyEndMonth);
        // Adjust FY end to the same fiscal year (start month determines
        // which calendar year the period sits in).
        if (company.FiscalYearStartMonth != 1)
        {
            fyEnd = LastDayOfMonth(year + 1, fyEndMonth);
            // If FY start = July 2026, FY end = June 2027 — but for
            // YEAR=2026 we still want THIS fiscal year's annual
            // return, which is the period ending in 2027.
        }
        var annualKind = company.TaxRegime == TaxRegime.Standard
            ? ComplianceObligationKind.IncomeTaxAnnual
            : ComplianceObligationKind.TurnoverTaxAnnual;
        var annualDue = AddMonths(fyEnd, 4);
        result.Add(new ComplianceObligation(
            annualKind,
            periodYear: year,
            periodOrdinal: 1,
            periodStart: fyStart,
            periodEnd: fyEnd,
            dueDate: annualDue));

        return result
            .OrderBy(o => o.DueDate)
            .ThenBy(o => o.Kind)
            .ToList();
    }

    private static DateOnly AddMonths(DateOnly d, int months)
    {
        var added = d.AddMonths(months);
        return added;
    }

    private static DateOnly LastDayOfMonth(int year, int month)
        => new(year, month, DateTime.DaysInMonth(year, month));
}
