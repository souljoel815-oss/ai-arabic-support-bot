namespace EgyptTax.Application.Tax;

/// <summary>
/// G3.4 — Egyptian Income Tax Law (Law 91/2005, as amended through
/// 2023). Standard regime: progressive brackets on annual taxable
/// income. The Law-6 simplified turnover regime is handled by
/// <see cref="TurnoverTaxBracketCalculator"/> (single bracket on
/// gross revenue, no add-backs).
///
/// Brackets effective FY2025 onward:
/// <code>
///   ≤ 40,000          → 0%
///   40,001-55,000     → 10%
///   55,001-70,000     → 15%
///   70,001-200,000    → 20%
///   200,001-400,000   → 22.5%
///   400,001-1,200,000 → 25%
///   &gt; 1,200,000    → 27.5%
/// </code>
///
/// Marginal-rate scheme: the rate applies only to the slice falling
/// inside the bracket, not the whole income. Each bracket's
/// contribution is <c>(min(income, ceiling) - floor) * rate</c>.
///
/// When the regulator publishes adjusted thresholds (annual practice
/// in Egypt — last revised by Law 175/2023), update <see cref="Brackets"/>
/// and the new rates flow through every consumer (return generator,
/// dashboard banner, payslip preview).
/// </summary>
public static class CorporateIncomeTaxBracketCalculator
{
    public static readonly IncomeTaxBracket[] Brackets =
    {
        new(LowerBoundEgp:           0m, UpperBoundEgp:    40_000m, RatePercent:  0m),
        new(LowerBoundEgp:      40_000m, UpperBoundEgp:    55_000m, RatePercent: 10m),
        new(LowerBoundEgp:      55_000m, UpperBoundEgp:    70_000m, RatePercent: 15m),
        new(LowerBoundEgp:      70_000m, UpperBoundEgp:   200_000m, RatePercent: 20m),
        new(LowerBoundEgp:     200_000m, UpperBoundEgp:   400_000m, RatePercent: 22.5m),
        new(LowerBoundEgp:     400_000m, UpperBoundEgp: 1_200_000m, RatePercent: 25m),
        new(LowerBoundEgp:   1_200_000m, UpperBoundEgp: decimal.MaxValue, RatePercent: 27.5m),
    };

    /// <summary>
    /// Compute the income-tax owed on the supplied annual taxable
    /// income. A loss (negative) returns zero — Egypt allows loss
    /// carry-forward but the *current* year's tax due can't be
    /// negative (a refund only arises through the WHT credit
    /// mechanism, which is handled separately via Form 41).
    /// </summary>
    public static IncomeTaxResult Compute(decimal annualTaxableIncomeEgp)
    {
        if (annualTaxableIncomeEgp <= 0m)
            return new IncomeTaxResult(0m, 0m, 0m, Array.Empty<IncomeTaxBracketContribution>());

        var contributions = new List<IncomeTaxBracketContribution>(Brackets.Length);
        decimal totalTax = 0m;

        foreach (var b in Brackets)
        {
            if (annualTaxableIncomeEgp <= b.LowerBoundEgp) break;

            var sliceCeiling = Math.Min(annualTaxableIncomeEgp, b.UpperBoundEgp);
            var sliceWidth = sliceCeiling - b.LowerBoundEgp;
            var sliceTax = decimal.Round(sliceWidth * b.RatePercent / 100m, 2, MidpointRounding.ToEven);

            contributions.Add(new IncomeTaxBracketContribution(
                LowerBoundEgp: b.LowerBoundEgp,
                UpperBoundEgp: b.UpperBoundEgp,
                RatePercent: b.RatePercent,
                TaxableSliceEgp: sliceWidth,
                TaxFromSliceEgp: sliceTax));

            totalTax += sliceTax;
        }

        // Effective rate is the blended rate the operator actually
        // pays — useful for the dashboard "you paid 18.4% overall"
        // line vs. the 27.5% top marginal.
        var effectiveRate = annualTaxableIncomeEgp > 0m
            ? decimal.Round(totalTax * 100m / annualTaxableIncomeEgp, 2, MidpointRounding.ToEven)
            : 0m;

        return new IncomeTaxResult(
            AnnualTaxableIncomeEgp: annualTaxableIncomeEgp,
            TaxOwedEgp: totalTax,
            EffectiveRatePercent: effectiveRate,
            Contributions: contributions);
    }
}

public sealed record IncomeTaxBracket(
    decimal LowerBoundEgp,
    decimal UpperBoundEgp,
    decimal RatePercent);

public sealed record IncomeTaxBracketContribution(
    decimal LowerBoundEgp,
    decimal UpperBoundEgp,
    decimal RatePercent,
    decimal TaxableSliceEgp,
    decimal TaxFromSliceEgp);

public sealed record IncomeTaxResult(
    decimal AnnualTaxableIncomeEgp,
    decimal TaxOwedEgp,
    decimal EffectiveRatePercent,
    IReadOnlyList<IncomeTaxBracketContribution> Contributions);
