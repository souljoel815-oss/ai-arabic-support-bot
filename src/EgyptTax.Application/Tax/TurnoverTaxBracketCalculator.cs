namespace EgyptTax.Application.Tax;

/// <summary>
/// Day 8 / Law 6 of 2025 — Egyptian "small enterprise" simplified
/// turnover-tax brackets. Replaces full corporate income tax for
/// businesses under the EGP 15M annual revenue cap; rate climbs with
/// the bracket so small operators get the lightest treatment:
///
/// <code>
///   ≤ 500,000        → 0.4%
///   500,001-2,000,000  → 0.6%
///   2,000,001-5,000,000  → 0.8%
///   5,000,001-10,000,000 → 1.0%
///   10,000,001-15,000,000 → 1.5%
///   &gt; 15,000,000   → ineligible (must use Standard regime)
/// </code>
///
/// The brackets are encoded here so the rate table is a single
/// auditable source — when the regulator publishes adjusted thresholds
/// (annual practice in Egypt), update <see cref="Brackets"/> and the
/// new rates flow through every consumer (report, dashboard banner,
/// payslip preview).
/// </summary>
public static class TurnoverTaxBracketCalculator
{
    public const decimal EligibilityCapEgp = 15_000_000m;

    public static readonly TurnoverTaxBracket[] Brackets =
    {
        new(UpperBoundEgp:    500_000m, RatePercent: 0.4m),
        new(UpperBoundEgp:  2_000_000m, RatePercent: 0.6m),
        new(UpperBoundEgp:  5_000_000m, RatePercent: 0.8m),
        new(UpperBoundEgp: 10_000_000m, RatePercent: 1.0m),
        new(UpperBoundEgp: 15_000_000m, RatePercent: 1.5m),
    };

    /// <summary>
    /// Compute the simplified-regime turnover tax owed on a given
    /// revenue figure. Returns <c>null</c> when the company is over
    /// the EGP 15M cap (must switch to the Standard regime — the
    /// dashboard surfaces the same nudge).
    /// </summary>
    public static TurnoverTaxResult? Compute(decimal annualRevenueEgp)
    {
        if (annualRevenueEgp < 0m) annualRevenueEgp = 0m;
        if (annualRevenueEgp > EligibilityCapEgp) return null;

        foreach (var b in Brackets)
        {
            if (annualRevenueEgp <= b.UpperBoundEgp)
            {
                var tax = decimal.Round(annualRevenueEgp * b.RatePercent / 100m, 2, MidpointRounding.ToEven);
                return new TurnoverTaxResult(
                    AnnualRevenueEgp: annualRevenueEgp,
                    AppliedRatePercent: b.RatePercent,
                    BracketCeilingEgp: b.UpperBoundEgp,
                    TaxOwedEgp: tax);
            }
        }

        // Fall-through (shouldn't happen because the last bracket
        // ends at the eligibility cap — defensive).
        return null;
    }
}

public sealed record TurnoverTaxBracket(decimal UpperBoundEgp, decimal RatePercent);

public sealed record TurnoverTaxResult(
    decimal AnnualRevenueEgp,
    decimal AppliedRatePercent,
    decimal BracketCeilingEgp,
    decimal TaxOwedEgp);
