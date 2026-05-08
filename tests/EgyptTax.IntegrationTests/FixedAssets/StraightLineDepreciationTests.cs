using EgyptTax.Application.FixedAssets;
using EgyptTax.Domain.Documents;
using EgyptTax.SharedKernel;

namespace EgyptTax.IntegrationTests.FixedAssets;

/// <summary>
/// T180 / US6 scenario 1 — straight-line depreciation contract.
/// Capitalize a 100,000 EGP asset on day 1 of fiscal year, 5-year
/// useful life, salvage 0, FullMonth convention. After 12 monthly
/// depreciation runs the schedule MUST add to ~20,000 EGP and the
/// remaining net book value MUST be ~80,000 EGP — that's the
/// "year-end taxable income shows 20,000 EGP depreciation expense
/// + NBV 80,000" promise from US6 spec.
///
/// Lives under tests/EgyptTax.IntegrationTests/FixedAssets/ per the
/// T180 spec path; the engine is pure-stateless so the test
/// doesn't actually need DB. Located here to keep the spec-mandated
/// file structure intact + so future iterations that DO need DB
/// (e.g., the Hangfire job assembling actual Expense rows) live in
/// the same namespace.
/// </summary>
public class StraightLineDepreciationTests
{
    [Fact]
    public void Year1_Of_100kAsset_5YearSL_FullMonth_Salvage0_Depreciates_Around20k_NbvAround80k()
    {
        var asset = FixedAsset.CreateDraft(
            code: "FA-001",
            description: new ArabicEnglishText("معدات", "Test machinery"),
            assetCategory: "Equipment",
            cost: MoneyEgp.From(100_000m),
            inServiceDate: new DateOnly(2026, 1, 1),
            usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero,
            convention: DepreciationConvention.FullMonth
        );
        asset.PutInService();

        // Year-1 schedule (Jan 2026 → Dec 2026 inclusive).
        var year1 = DepreciationEngine.ComputeMonthlySchedule(
            asset,
            fromMonth: new DateOnly(2026, 1, 1),
            throughMonth: new DateOnly(2026, 12, 1)
        );

        year1
            .Should()
            .HaveCount(
                12,
                because: "12 months of straight-line depreciation across the first fiscal year"
            );

        var year1Total = year1.Sum(l => l.Amount.Amount);
        year1Total
            .Should()
            .BeApproximately(
                20_000m,
                precision: 1m,
                because: "100k / 5 years = 20k/year (within rounding tolerance — 100k/60 = 1666.67 monthly × 12 ≈ 20,000.04)"
            );

        // NBV at end of year 1.
        var nbvAtYearEnd = DepreciationEngine.NetBookValueAt(asset, new DateOnly(2026, 12, 31));
        nbvAtYearEnd
            .Amount.Should()
            .BeApproximately(
                80_000m,
                precision: 1m,
                because: "FR-018 — NBV after one year of 5-year SL on a 100k asset is ~80k"
            );
    }

    [Fact]
    public void FullSchedule_Across5Years_SumsExactlyToCostMinusSalvage()
    {
        // The remainder-absorption rule means cumulative depreciation
        // MUST equal cost - salvage exactly at end-of-life regardless
        // of per-month rounding. This is what keeps the books from
        // drifting off-balance over the asset's lifetime.
        var asset = FixedAsset.CreateDraft(
            code: "FA-002",
            description: new ArabicEnglishText("معدات", "Test asset"),
            assetCategory: "Equipment",
            cost: MoneyEgp.From(100_000m),
            inServiceDate: new DateOnly(2026, 1, 1),
            usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero,
            convention: DepreciationConvention.FullMonth
        );
        asset.PutInService();

        var full = DepreciationEngine.ComputeFullSchedule(asset);
        full.Should().HaveCount(60);
        full.Sum(l => l.Amount.Amount)
            .Should()
            .Be(
                100_000m,
                because: "the rounding remainder MUST be absorbed into the last period so lifetime depreciation = cost − salvage exactly"
            );

        // NBV at end of life equals salvage (zero here).
        var nbvAtLifeEnd = DepreciationEngine.NetBookValueAt(asset, new DateOnly(2030, 12, 31));
        nbvAtLifeEnd.Amount.Should().Be(0m);
    }

    [Fact]
    public void Salvage_Value_Reduces_The_Depreciable_Base()
    {
        // Same asset but with 10k salvage; depreciable base is now
        // 90k → 18k/year, NBV at end of life = 10k (salvage floor).
        var asset = FixedAsset.CreateDraft(
            code: "FA-003",
            description: new ArabicEnglishText("معدات", "Test asset"),
            assetCategory: "Equipment",
            cost: MoneyEgp.From(100_000m),
            inServiceDate: new DateOnly(2026, 1, 1),
            usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.From(10_000m),
            convention: DepreciationConvention.FullMonth
        );
        asset.PutInService();

        var full = DepreciationEngine.ComputeFullSchedule(asset);
        full.Sum(l => l.Amount.Amount)
            .Should()
            .Be(90_000m, because: "depreciable base = cost − salvage = 100k − 10k = 90k");

        DepreciationEngine
            .NetBookValueAt(asset, new DateOnly(2030, 12, 31))
            .Amount.Should()
            .Be(10_000m, because: "FR-018 — NBV bottoms out at salvage value, never below");
    }

    [Fact]
    public void DraftAsset_HasEmptySchedule()
    {
        // Draft assets aren't depreciable until PutInService runs.
        var asset = FixedAsset.CreateDraft(
            code: "FA-004",
            description: new ArabicEnglishText("معدات", "Test"),
            assetCategory: "Equipment",
            cost: MoneyEgp.From(100_000m),
            inServiceDate: new DateOnly(2026, 1, 1),
            usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero,
            convention: DepreciationConvention.FullMonth
        );

        DepreciationEngine
            .ComputeFullSchedule(asset)
            .Should()
            .BeEmpty(
                because: "Draft assets aren't depreciable yet — the schedule is empty until PutInService transitions to InService"
            );
    }
}
