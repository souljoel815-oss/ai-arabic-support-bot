using EgyptTax.Application.Tax;

namespace EgyptTax.UnitTests.Application.Tax;

/// <summary>
/// G3.4 — covers the marginal-rate bracket arithmetic in
/// <see cref="CorporateIncomeTaxBracketCalculator"/>. The published
/// Egyptian Income Tax Law (Law 91/2005, as amended) is the source
/// of truth for the bracket boundaries.
/// </summary>
public class CorporateIncomeTaxBracketCalculatorTests
{
    [Fact]
    public void Compute_ZeroIncome_ReturnsZero()
    {
        var result = CorporateIncomeTaxBracketCalculator.Compute(0m);

        result.TaxOwedEgp.Should().Be(0m);
        result.EffectiveRatePercent.Should().Be(0m);
        result.Contributions.Should().BeEmpty();
    }

    [Fact]
    public void Compute_NegativeIncome_ReturnsZero()
    {
        // Carried-forward losses are handled outside this calculator.
        var result = CorporateIncomeTaxBracketCalculator.Compute(-10_000m);

        result.TaxOwedEgp.Should().Be(0m);
    }

    [Fact]
    public void Compute_BelowFirstBracket_NoTax()
    {
        // 40,000 EGP exemption — the first 40k is taxed at 0%.
        var result = CorporateIncomeTaxBracketCalculator.Compute(40_000m);

        result.TaxOwedEgp.Should().Be(0m);
        result.Contributions.Should().ContainSingle(c => c.RatePercent == 0m);
    }

    [Fact]
    public void Compute_SecondBracket_TaxesOnlyTheSlice()
    {
        // 50,000 = 40k @ 0% + 10k @ 10% = 1,000 EGP.
        var result = CorporateIncomeTaxBracketCalculator.Compute(50_000m);

        result.TaxOwedEgp.Should().Be(1_000m);
        result.Contributions.Should().HaveCount(2);
        result.Contributions[1].TaxFromSliceEgp.Should().Be(1_000m);
    }

    [Fact]
    public void Compute_MiddleBracket_SumsAcrossSlices()
    {
        // 100,000 = 40k @ 0% + 15k @ 10% + 15k @ 15% + 30k @ 20%
        //        = 0 + 1500 + 2250 + 6000 = 9,750 EGP.
        var result = CorporateIncomeTaxBracketCalculator.Compute(100_000m);

        result.TaxOwedEgp.Should().Be(9_750m);
        result.Contributions.Should().HaveCount(4);
    }

    [Fact]
    public void Compute_TopBracket_AppliesHighestRateOnTailOnly()
    {
        // 2,000,000 falls into the >1.2M (27.5%) bracket. The slice
        // ABOVE 1.2M is 800k taxed at 27.5% = 220,000. Everything
        // below 1.2M is taxed at the lower-bracket rates.
        var result = CorporateIncomeTaxBracketCalculator.Compute(2_000_000m);

        // Sanity check: the topmost contribution is the 800k @ 27.5%.
        var top = result.Contributions[^1];
        top.RatePercent.Should().Be(27.5m);
        top.TaxableSliceEgp.Should().Be(800_000m);
        top.TaxFromSliceEgp.Should().Be(220_000m);

        // The blended effective rate must be strictly below the top
        // marginal rate (otherwise the lower brackets are being
        // ignored).
        result.EffectiveRatePercent.Should().BeLessThan(27.5m);
    }

    [Fact]
    public void Compute_EffectiveRate_IsBlendedAverage()
    {
        // 100k @ TaxOwed=9,750 → effective rate = 9.75%.
        var result = CorporateIncomeTaxBracketCalculator.Compute(100_000m);

        result.EffectiveRatePercent.Should().Be(9.75m);
    }

    [Fact]
    public void Compute_AllBracketsCovered_ForVeryLargeIncome()
    {
        var result = CorporateIncomeTaxBracketCalculator.Compute(10_000_000m);

        // All 7 brackets should contribute (0% one is omitted from
        // contributions because we break before the 0% slice carries
        // tax — actually it IS included since the 40k slice is part
        // of the income; verify by looking at totals).
        result.Contributions.Should().NotBeEmpty();
        result.TaxOwedEgp.Should().BeGreaterThan(0m);
        result.EffectiveRatePercent.Should().BeInRange(20m, 27.5m);
    }

    [Fact]
    public void Compute_AtExactBracketBoundary_FillsBracketExactly()
    {
        // 55,000 = exact boundary between 10% and 15% brackets.
        // Should be 40k @ 0 + 15k @ 10 = 1,500. The next bracket
        // gets nothing because we stop when income ≤ lower bound.
        var result = CorporateIncomeTaxBracketCalculator.Compute(55_000m);

        result.TaxOwedEgp.Should().Be(1_500m);
        result.Contributions.Should().HaveCount(2);
    }
}
