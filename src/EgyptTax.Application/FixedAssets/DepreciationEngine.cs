using EgyptTax.Domain.Documents;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.FixedAssets;

/// <summary>
/// FR-017 / FR-018 / US6 — pure-stateless depreciation calculator.
/// No DB, no clock, no DI — exposes <see cref="ComputeMonthlySchedule"/>
/// + <see cref="NetBookValueAt"/> as static methods so it's
/// trivially unit-testable and the Hangfire monthly job (T185) can
/// call it without a scope.
///
/// MVP scope: straight-line method only (FR-017 Phase 4 minimum);
/// FullMonth + MidMonth conventions. Other methods + conventions
/// slot in via match arms when the surface needs them.
///
/// Rounding: per-period amounts rounded to 2 dp ToEven (banker's
/// rounding), with the rounding remainder absorbed into the LAST
/// depreciable period so the lifetime total equals
/// <c>cost - salvage</c> exactly (no drift). Disposal cuts the
/// schedule at the disposal month.
/// </summary>
public static class DepreciationEngine
{
    /// <summary>
    /// One row in the depreciation schedule. <paramref name="Period"/>
    /// is normalised to the first day of the month the depreciation
    /// applies to.
    /// </summary>
    public sealed record DepreciationLine(DateOnly Period, MoneyEgp Amount);

    /// <summary>
    /// Compute the per-month depreciation schedule for the asset
    /// from its in-service date through the END of its useful life
    /// (or its disposal date, whichever comes first).
    /// </summary>
    public static IReadOnlyList<DepreciationLine> ComputeFullSchedule(FixedAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.Status == FixedAssetStatus.Draft)
        {
            // Draft assets aren't depreciable yet — the schedule is
            // empty. Once PutInService runs, the schedule is computed
            // from the in-service date forward.
            return Array.Empty<DepreciationLine>();
        }

        var depreciable = asset.Cost.Amount - asset.SalvageValue.Amount;
        if (depreciable <= 0m)
        {
            return Array.Empty<DepreciationLine>();
        }

        var monthlyRaw = depreciable / asset.UsefulLifeMonths;
        var monthlyRounded = decimal.Round(monthlyRaw, 2, MidpointRounding.ToEven);

        // Build the unfiltered schedule first; we'll trim by disposal
        // and absorb the rounding remainder at the end.
        var schedule = new List<DepreciationLine>(asset.UsefulLifeMonths);
        var firstMonth = MonthOf(asset.InServiceDate);

        var allocated = 0m;
        for (var i = 0; i < asset.UsefulLifeMonths; i++)
        {
            var period = firstMonth.AddMonths(i);
            decimal amount;

            if (i == 0 && asset.Convention == DepreciationConvention.MidMonth)
            {
                // Mid-month: half a month in the in-service month.
                amount = decimal.Round(monthlyRounded / 2m, 2, MidpointRounding.ToEven);
            }
            else
            {
                amount = monthlyRounded;
            }

            schedule.Add(new DepreciationLine(period, MoneyEgp.From(amount)));
            allocated += amount;
        }

        // Mid-month convention: the leftover half-month tail rolls
        // into one extra period after the nominal useful-life end.
        if (asset.Convention == DepreciationConvention.MidMonth)
        {
            var tailAmount = decimal.Round(monthlyRounded / 2m, 2, MidpointRounding.ToEven);
            schedule.Add(new DepreciationLine(
                firstMonth.AddMonths(asset.UsefulLifeMonths),
                MoneyEgp.From(tailAmount)));
            allocated += tailAmount;
        }

        // Absorb the rounding remainder into the LAST depreciable
        // period so SUM(amounts) == cost - salvage exactly. Without
        // this the cumulative depreciation would drift by a few
        // cents over the useful life — small but real.
        var remainder = depreciable - allocated;
        if (remainder != 0m && schedule.Count > 0)
        {
            var last = schedule[^1];
            schedule[^1] = new DepreciationLine(
                last.Period,
                MoneyEgp.From(decimal.Round(last.Amount.Amount + remainder, 2, MidpointRounding.ToEven)));
        }

        // Trim by disposal date (depreciation stops at the disposal
        // month — the disposal month itself still gets its scheduled
        // amount in the MVP cut; the ATA-style "half month at
        // disposal" mid-month tail kicks in if/when needed).
        if (asset.DisposedOn is { } disposed)
        {
            var disposalMonth = MonthOf(disposed);
            schedule = schedule.Where(l => l.Period <= disposalMonth).ToList();
        }

        return schedule;
    }

    /// <summary>
    /// Convenience: schedule lines whose period falls inside
    /// [fromMonth, throughMonth] inclusive. Both bounds are
    /// normalised to first-of-month.
    /// </summary>
    public static IReadOnlyList<DepreciationLine> ComputeMonthlySchedule(
        FixedAsset asset, DateOnly fromMonth, DateOnly throughMonth)
    {
        var from = MonthOf(fromMonth);
        var through = MonthOf(throughMonth);
        if (through < from)
        {
            throw new ArgumentException(
                $"throughMonth {through:yyyy-MM-dd} cannot precede fromMonth {from:yyyy-MM-dd}.",
                nameof(throughMonth));
        }
        return ComputeFullSchedule(asset)
            .Where(l => l.Period >= from && l.Period <= through)
            .ToList();
    }

    /// <summary>
    /// FR-018 — net book value at <paramref name="asOfDate"/>:
    /// cost minus the SUM of every scheduled depreciation amount
    /// whose period is on or before the as-of month. Bounded below
    /// by salvage value so the NBV never undershoots.
    /// </summary>
    public static MoneyEgp NetBookValueAt(FixedAsset asset, DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var asOfMonth = MonthOf(asOfDate);
        var depreciatedToDate = ComputeFullSchedule(asset)
            .Where(l => l.Period <= asOfMonth)
            .Sum(l => l.Amount.Amount);
        var nbv = asset.Cost.Amount - depreciatedToDate;
        if (nbv < asset.SalvageValue.Amount) nbv = asset.SalvageValue.Amount;
        return MoneyEgp.From(decimal.Round(nbv, 2, MidpointRounding.ToEven));
    }

    private static DateOnly MonthOf(DateOnly d) => new(d.Year, d.Month, 1);
}
