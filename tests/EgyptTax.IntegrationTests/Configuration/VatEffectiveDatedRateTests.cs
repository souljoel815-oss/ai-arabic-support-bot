using EgyptTax.Application.Configuration;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Configuration;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;

namespace EgyptTax.IntegrationTests.Configuration;

/// <summary>
/// T173 / FR-019 / FR-022 / US5 — VAT-rate change MUST apply per
/// document date. The Standard category is seeded at 14% from
/// 2026-01-01; an Administrator inserts a new row with the same
/// code at 15% effective 2026-07-01. Lookups before 2026-07-01
/// resolve to 14%; lookups on/after resolve to 15%. Pins the
/// "supersede by inserting newer" model — historical invoices
/// always recompute against the rate in force on their document
/// date.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class VatEffectiveDatedRateTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task VatRateChange_AppliesPerDocumentDate_PerFr022()
    {
        await using var db = await _fixture.CreateContextAsync();

        // Old rate: Standard 14% effective Jan 1, capped Jun 30.
        var oldRate = new VatCategory(
            code: "Standard-T173",
            name: new ArabicEnglishText("قياسي", "Standard (14%)"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: new DateOnly(2026, 6, 30),
            recoverableInputVat: true
        );
        // New rate: Standard 15% from Jul 1 (open-ended).
        var newRate = new VatCategory(
            code: "Standard-T173",
            name: new ArabicEnglishText("قياسي", "Standard (15%)"),
            ratePercent: 15m,
            effectiveFromDate: new DateOnly(2026, 7, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        db.Add(oldRate);
        db.Add(newRate);
        await db.SaveChangesAsync();

        var lookup = new SqlVatRateLookup(db);

        // Document dated June 15 → 14%.
        var june = await lookup.GetEffectiveAsync("Standard-T173", new DateOnly(2026, 6, 15));
        june.Should().NotBeNull();
        june!
            .RatePercent.Should()
            .Be(
                14m,
                because: "FR-022 — June document picks the rate effective on its document date (14%)"
            );
        june.Id.Should().Be(oldRate.Id);

        // Document dated July 15 → 15% (the supersession).
        var july = await lookup.GetEffectiveAsync("Standard-T173", new DateOnly(2026, 7, 15));
        july.Should().NotBeNull();
        july!.RatePercent.Should().Be(15m, because: "the new row covers Jul 1 onwards");
        july.Id.Should().Be(newRate.Id);

        // Date before either row → null (caller treats as "no rate effective").
        var early = await lookup.GetEffectiveAsync("Standard-T173", new DateOnly(2025, 12, 15));
        early
            .Should()
            .BeNull(
                because: "no row's effective window covers 2025-12-15 — the operator hasn't configured one yet"
            );
    }

    [Fact]
    public async Task ListByCode_ReturnsAllRowsForOverlapVisualisation_OrderedByEffectiveFrom()
    {
        // T175 settings page uses ListByCode to surface the full
        // supersession history + flag operator-error overlapping
        // rows visually.
        await using var db = await _fixture.CreateContextAsync();

        var jan = new VatCategory(
            code: "Reduced-T173",
            name: new ArabicEnglishText("مخفض", "Reduced (5%)"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: new DateOnly(2026, 6, 30),
            recoverableInputVat: true
        );
        var jul = new VatCategory(
            code: "Reduced-T173",
            name: new ArabicEnglishText("مخفض", "Reduced (7%)"),
            ratePercent: 7m,
            effectiveFromDate: new DateOnly(2026, 7, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        db.Add(jul);
        db.Add(jan); // intentionally inserted out of order
        await db.SaveChangesAsync();

        var rows = await new SqlVatRateLookup(db).ListByCodeAsync("Reduced-T173");

        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be(jan.Id, because: "ListByCode orders by EffectiveFromDate ascending");
        rows[1].Id.Should().Be(jul.Id);
    }
}
