using EgyptTax.Application.Wht;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Wht;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;

namespace EgyptTax.IntegrationTests.Wht;

/// <summary>
/// T197 / R-17 / FR-045 — WHT compute MUST select the category in
/// force on the **payment date** (not the invoice date — Egyptian
/// WHT is event-dated to the cash flow). When two categories with
/// the same code are seeded with different effective windows, a
/// payment in window A picks rate A; a payment in window B picks
/// rate B. Catches a future regression that accidentally drives
/// off invoice-date.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class WhtPaymentDateEffectivityTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task PaymentDate_DrivesCategorySelection_NotInvoiceDate()
    {
        await using var db = await _fixture.CreateContextAsync();

        // "Services" at 5% Jan 1 → Jun 30 2026; superseded by 10%
        // from Jul 1 2026.
        var oldRate = new WhtCategory(
            code: "Services",
            name: new ArabicEnglishText("خدمات", "Services (5%)"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: new DateOnly(2026, 6, 30),
            applicableTo: WhtApplicableTo.SuppliersServices);
        var newRate = new WhtCategory(
            code: "Services",
            name: new ArabicEnglishText("خدمات", "Services (10%)"),
            ratePercent: 10m,
            effectiveFromDate: new DateOnly(2026, 7, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices);
        db.Add(oldRate); db.Add(newRate);
        await db.SaveChangesAsync();

        var service = new SqlWhtComputeService(db);

        // Payment in June → 5% applies.
        var june = await service.ComputeAsync(
            "Services", new DateOnly(2026, 6, 15),
            MoneyEgp.From(10_000m), WhtApplicableTo.SuppliersServices);
        june.Should().NotBeNull();
        june!.RateAppliedPercent.Should().Be(5m);
        june.AmountWithheld.Amount.Should().Be(500m,
            because: "10k × 5% = 500 — June payment falls in the old rate's window");
        june.WhtCategoryId.Should().Be(oldRate.Id);

        // Payment in August → 10% applies even if the underlying
        // invoice was issued in June. R-17 — payment date wins.
        var august = await service.ComputeAsync(
            "Services", new DateOnly(2026, 8, 1),
            MoneyEgp.From(10_000m), WhtApplicableTo.SuppliersServices);
        august.Should().NotBeNull();
        august!.RateAppliedPercent.Should().Be(10m);
        august.AmountWithheld.Amount.Should().Be(1_000m,
            because: "10k × 10% = 1,000 — August payment falls in the new rate's window");
        august.WhtCategoryId.Should().Be(newRate.Id);
    }

    [Fact]
    public async Task PaymentDate_BeforeAnyEffectiveCategory_ReturnsNull()
    {
        await using var db = await _fixture.CreateContextAsync();

        var category = new WhtCategory(
            code: "Services-Future",
            name: new ArabicEnglishText("خدمات", "Services future"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2027, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices);
        db.Add(category);
        await db.SaveChangesAsync();

        var service = new SqlWhtComputeService(db);

        // Payment in 2026 → category not yet effective → no WHT.
        var result = await service.ComputeAsync(
            "Services-Future", new DateOnly(2026, 6, 15),
            MoneyEgp.From(1_000m), WhtApplicableTo.SuppliersServices);

        result.Should().BeNull(
            because: "no category is effective on the payment date — caller treats this as 'WHT not applicable'");
    }

    [Fact]
    public async Task ApplicableTo_FiltersOutWrongDirection()
    {
        await using var db = await _fixture.CreateContextAsync();

        // Customers-only category — supplier-side payment MUST NOT
        // pick it up.
        var category = new WhtCategory(
            code: "Cust-Only",
            name: new ArabicEnglishText("عميل فقط", "Customers only"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.CustomersServices);
        db.Add(category);
        await db.SaveChangesAsync();

        var service = new SqlWhtComputeService(db);

        var supplierSide = await service.ComputeAsync(
            "Cust-Only", new DateOnly(2026, 6, 1),
            MoneyEgp.From(1_000m), WhtApplicableTo.SuppliersServices);
        supplierSide.Should().BeNull();

        var customerSide = await service.ComputeAsync(
            "Cust-Only", new DateOnly(2026, 6, 1),
            MoneyEgp.From(1_000m), WhtApplicableTo.CustomersServices);
        customerSide.Should().NotBeNull();
        customerSide!.RateAppliedPercent.Should().Be(5m);
    }

    [Fact]
    public async Task ApplicableTo_Both_MatchesEitherDirection()
    {
        await using var db = await _fixture.CreateContextAsync();

        var category = new WhtCategory(
            code: "Universal",
            name: new ArabicEnglishText("عام", "Universal"),
            ratePercent: 3m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.Both);
        db.Add(category);
        await db.SaveChangesAsync();

        var service = new SqlWhtComputeService(db);

        var supplierSide = await service.ComputeAsync(
            "Universal", new DateOnly(2026, 6, 1),
            MoneyEgp.From(1_000m), WhtApplicableTo.SuppliersServices);
        var customerSide = await service.ComputeAsync(
            "Universal", new DateOnly(2026, 6, 1),
            MoneyEgp.From(1_000m), WhtApplicableTo.CustomersServices);

        supplierSide.Should().NotBeNull();
        customerSide.Should().NotBeNull();
        supplierSide!.RateAppliedPercent.Should().Be(3m);
        customerSide!.RateAppliedPercent.Should().Be(3m);
    }
}
