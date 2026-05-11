using EgyptTax.Application.Reports;
using EgyptTax.Application.Tax;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Periods;
using EgyptTax.Domain.Tax;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Tax;

/// <summary>
/// G3.4 — produces an <see cref="IncomeTaxReturn"/> for a fiscal
/// year. The fiscal-year window is derived from the company's
/// configured <c>FiscalYearStartMonth</c>; the bracket-based tax
/// calculation flows from the configured <c>TaxRegime</c>.
///
/// Gate: every VAT month inside the fiscal year MUST be Locked.
/// Same justification as <see cref="GenerateVatReturnHandler"/> —
/// drafts, missing attachments, and failed ETA submissions can't
/// leak into the return. Re-generation produces a NEW row; the
/// list page shows the latest by FiscalYear desc + GeneratedAt desc.
/// </summary>
public sealed class GenerateIncomeTaxReturnHandler
{
    private readonly AppDbContext _db;
    private readonly ITaxableIncomeReportQuery _report;
    private readonly IClock _clock;

    public GenerateIncomeTaxReturnHandler(
        AppDbContext db,
        ITaxableIncomeReportQuery report,
        IClock clock)
    {
        _db = db;
        _report = report;
        _clock = clock;
    }

    public async Task<IncomeTaxReturn> GenerateAsync(
        GenerateIncomeTaxReturnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.GeneratedByUserId == Guid.Empty)
            throw new ArgumentException("GeneratedByUserId required.", nameof(command));
        if (command.FiscalYear is < 1900 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(command), "FiscalYear out of range.");

        // Load company so we can (a) derive the fiscal-year window
        // from FiscalYearStartMonth and (b) pick the right tax regime
        // (Standard vs Law-6 simplified).
        var company = await _db.Set<Company>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Company profile not configured. Open Settings → Company before generating returns.");

        var (periodStart, periodEnd) = DeriveFiscalYearWindow(command.FiscalYear, company.FiscalYearStartMonth);

        // Period-lock gate — same pattern as VAT/Form 41. Walk every
        // VAT month inside the fiscal-year window and refuse if any
        // are still Open. This may span calendar years for non-Jan
        // fiscal starts, so we enumerate by (year, month).
        await EnsureAllVatMonthsLockedAsync(periodStart, periodEnd, cancellationToken);

        var report = await _report.RunAsync(periodStart, periodEnd, cancellationToken);

        // Apply the regime-specific tax computation. Standard uses
        // the bracket calculator on TaxableIncome; Law-6 uses the
        // turnover calculator on Revenue.
        var regime = company.TaxRegime == TaxRegime.Law6Simplified
            ? IncomeTaxRegime.Law6Simplified
            : IncomeTaxRegime.Standard;

        decimal taxDueAmount;
        decimal effectiveRate;

        if (regime == IncomeTaxRegime.Law6Simplified)
        {
            var turnover = TurnoverTaxBracketCalculator.Compute(report.Revenue.Amount)
                ?? throw new InvalidOperationException(
                    $"Revenue of {report.Revenue.Amount:N2} EGP exceeds the EGP 15M Law-6 cap. " +
                    "Switch the company to the Standard regime in Settings → Company before generating.");
            taxDueAmount = turnover.TaxOwedEgp;
            effectiveRate = turnover.AppliedRatePercent;
        }
        else
        {
            var bracket = CorporateIncomeTaxBracketCalculator.Compute(report.TaxableIncome.Amount);
            taxDueAmount = bracket.TaxOwedEgp;
            effectiveRate = bracket.EffectiveRatePercent;
        }

        var nowUtc = _clock.UtcNow;
        var ret = new IncomeTaxReturn(
            regime: regime,
            fiscalYear: command.FiscalYear,
            periodStart: report.PeriodStart,
            periodEnd: report.PeriodEnd,
            revenue: report.Revenue,
            deductibleExpenses: report.DeductibleExpenses,
            nonDeductibleAdjustments: report.NonDeductibleAdjustments,
            managementProfitLoss: report.ManagementProfitLoss,
            taxableIncome: report.TaxableIncome,
            taxDue: MoneyEgp.From(taxDueAmount),
            effectiveRatePercent: effectiveRate,
            contributingDocumentCount: report.Rows.Count,
            generatedAtUtc: nowUtc,
            generatedByUserId: command.GeneratedByUserId);

        if (!string.IsNullOrWhiteSpace(command.Note))
        {
            ret.UpdateNote(command.Note);
        }

        _db.Add(ret);
        await _db.SaveChangesAsync(cancellationToken);
        return ret;
    }

    /// <summary>
    /// Egyptian fiscal years end on the FY label year. A Jan-start
    /// company reports FY 2026 = 2026-01-01 → 2026-12-31; a
    /// July-start company reports FY 2026 = 2025-07-01 → 2026-06-30.
    /// </summary>
    private static (DateOnly Start, DateOnly End) DeriveFiscalYearWindow(int fiscalYear, int fiscalYearStartMonth)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth));

        if (fiscalYearStartMonth == 1)
        {
            return (new DateOnly(fiscalYear, 1, 1), new DateOnly(fiscalYear, 12, 31));
        }

        var start = new DateOnly(fiscalYear - 1, fiscalYearStartMonth, 1);
        var end = start.AddYears(1).AddDays(-1);
        return (start, end);
    }

    private async Task EnsureAllVatMonthsLockedAsync(
        DateOnly windowStart,
        DateOnly windowEnd,
        CancellationToken cancellationToken)
    {
        var requiredMonths = new List<(int Year, int Month)>(12);
        var cursor = new DateOnly(windowStart.Year, windowStart.Month, 1);
        var lastMonth = new DateOnly(windowEnd.Year, windowEnd.Month, 1);
        while (cursor <= lastMonth)
        {
            requiredMonths.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        // Pull the VAT-month rows in one round-trip then check
        // membership in-memory; SQLite + the (year, month) tuple
        // doesn't translate to a single IN clause cleanly.
        var minYear = requiredMonths[0].Year;
        var maxYear = requiredMonths[^1].Year;
        var existing = await _db.Set<TaxPeriod>()
            .AsNoTracking()
            .Where(p => p.PeriodKind == TaxPeriodKind.VatMonth
                && p.Year >= minYear
                && p.Year <= maxYear)
            .ToListAsync(cancellationToken);

        var missing = new List<string>();
        foreach (var (y, m) in requiredMonths)
        {
            var period = existing.FirstOrDefault(p => p.Year == y && p.MonthOrQuarter == m);
            if (period is null || period.Status != TaxPeriodStatus.Locked)
            {
                missing.Add($"{y}-{m:D2}");
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot generate income-tax return for FY{windowEnd.Year}: the following VAT months are not Locked — " +
                string.Join(", ", missing) +
                ". Close them via the Closing Cockpit first.");
        }
    }
}

public sealed record GenerateIncomeTaxReturnCommand(
    int FiscalYear,
    Guid GeneratedByUserId,
    string? Note = null);
