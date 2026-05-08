using EgyptTax.Application.FixedAssets;
using EgyptTax.Domain.Documents;
using EgyptTax.SharedKernel;

namespace EgyptTax.IntegrationTests.FixedAssets;

/// <summary>
/// T181 / US6 scenario 2 — mid-month convention contract. An asset
/// placed in service on the 15th of a month MUST receive a HALF
/// month of depreciation in that month (and a tail half-month at
/// the end of useful life), so the lifetime total is still
/// <c>cost − salvage</c> exactly. Catches a future regression
/// where the convention is silently dropped or rounded the wrong
/// way.
/// </summary>
public class MidMonthConventionTests
{
    [Fact]
    public void AssetInService_OnThe15th_GetsHalfMonth_InTheInServiceMonth()
    {
        // 60,000 EGP / 60 months = 1,000/month full-month rate.
        // MidMonth convention: 500 in the in-service month, 1,000
        // for months 2..60, 500 in the tail month past useful life.
        var asset = FixedAsset.CreateDraft(
            code: "FA-MM-001",
            description: new ArabicEnglishText("معدات", "Mid-month asset"),
            assetCategory: "Equipment",
            cost: MoneyEgp.From(60_000m),
            inServiceDate: new DateOnly(2026, 5, 15),
            usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero,
            convention: DepreciationConvention.MidMonth);
        asset.PutInService();

        var full = DepreciationEngine.ComputeFullSchedule(asset);

        full.Should().HaveCount(61,
            because: "mid-month convention adds a tail half-month at the end of useful life");

        // In-service month — half a month.
        full[0].Period.Should().Be(new DateOnly(2026, 5, 1));
        full[0].Amount.Amount.Should().Be(500m,
            because: "the in-service month gets HALF a month of depreciation under MidMonth (1000 / 2 = 500)");

        // Months 2 through 60 — full month each.
        for (var i = 1; i < 60; i++)
        {
            full[i].Amount.Amount.Should().Be(1_000m,
                because: $"month {i + 1} of mid-month-convention depreciation MUST be the full monthly rate");
        }

        // Tail month at the end — the other half.
        full[^1].Period.Should().Be(new DateOnly(2031, 5, 1),
            because: "60 months from May 2026 lands the tail in May 2031");
        full[^1].Amount.Amount.Should().Be(500m);

        // Total still equals cost − salvage exactly.
        full.Sum(l => l.Amount.Amount).Should().Be(60_000m,
            because: "lifetime total MUST equal cost − salvage regardless of convention or rounding");
    }

    [Fact]
    public void MidMonth_NbvAtFirstYearEnd_LagsFullMonthBy_HalfAMonth()
    {
        // Same asset capitalized on the 15th of January 2026,
        // mid-month convention. Year-1 (Jan-Dec) should depreciate
        // 11.5 months × full rate = 11.5k (instead of 12k for
        // FullMonth). NBV at year-end accordingly higher by 500.
        var asset = FixedAsset.CreateDraft(
            code: "FA-MM-002",
            description: new ArabicEnglishText("معدات", "Mid-month asset"),
            assetCategory: "Equipment",
            cost: MoneyEgp.From(60_000m),
            inServiceDate: new DateOnly(2026, 1, 15),
            usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero,
            convention: DepreciationConvention.MidMonth);
        asset.PutInService();

        var year1 = DepreciationEngine.ComputeMonthlySchedule(
            asset,
            fromMonth: new DateOnly(2026, 1, 1),
            throughMonth: new DateOnly(2026, 12, 1));

        year1.Sum(l => l.Amount.Amount).Should().Be(11_500m,
            because: "11.5 months × 1000/month = 11,500 (half-month for January + full for Feb-Dec)");

        DepreciationEngine.NetBookValueAt(asset, new DateOnly(2026, 12, 31))
            .Amount.Should().Be(48_500m,
                because: "60k cost − 11.5k year-1 depreciation = 48.5k NBV");
    }
}
