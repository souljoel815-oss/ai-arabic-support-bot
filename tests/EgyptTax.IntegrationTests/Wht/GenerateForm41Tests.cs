using EgyptTax.Application.Audit;
using EgyptTax.Application.Payments;
using EgyptTax.Application.Purchases;
using EgyptTax.Application.Wht;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Tax;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Payments;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.Infrastructure.Wht;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Wht;

/// <summary>
/// US7 / FR-046 / T209 — Form 41 generator happy path. Posts two
/// supplier payments with WHT splits in Q2 2026 (different
/// suppliers, different categories), runs the generator, asserts:
///   * Payload's lines match the certificates 1:1.
///   * Per-category roll-up adds up.
///   * Reconciliation matches (WHT-payable accrual = sum of cert
///     withholdings) and `MatchesTotalAmountWithheld` is true.
///   * A persisted Form41Filing row exists in Unfiled status with
///     the right totals.
///   * Re-generating the same quarter is refused (one canonical
///     filing per quarter per FR-046).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class GenerateForm41Tests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Generate_AggregatesQuarterlyCertificates_IntoSchemaShapedPayload()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (vat, user) = await SeedSharedAsync(db);

        // Two WHT categories, one per supplier.
        var services5 = new WhtCategory(
            code: "Services",
            name: new ArabicEnglishText("خدمات", "Services"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices);
        var prof10 = new WhtCategory(
            code: "Professional",
            name: new ArabicEnglishText("مهني", "Professional"),
            ratePercent: 10m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices);
        db.Add(services5); db.Add(prof10);
        await db.SaveChangesAsync();

        // Post two supplier payments in Q2 2026 with WHT splits.
        await PostSupplierPaymentWithWhtAsync(db, user, vat,
            tinSuffix: "111", grossAmount: 10_000m,
            paymentDate: new DateOnly(2026, 5, 10),
            categoryCode: "Services");
        await PostSupplierPaymentWithWhtAsync(db, user, vat,
            tinSuffix: "222", grossAmount: 2_500m,
            paymentDate: new DateOnly(2026, 6, 20),
            categoryCode: "Professional");

        // Run the generator for Q2 2026.
        var clock = new TestClock(new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc));
        var handler = new GenerateForm41Handler(db, clock);
        var result = await handler.HandleAsync(
            new GenerateForm41Command(2026, 2, user.Id), CancellationToken.None);

        // Payload sanity.
        result.Payload.FilingHeader.FiscalYear.Should().Be(2026);
        result.Payload.FilingHeader.Quarter.Should().Be(2);
        result.Payload.FilingHeader.FillingPeriodStart.Should().Be(new DateOnly(2026, 4, 1));
        result.Payload.FilingHeader.FillingPeriodEnd.Should().Be(new DateOnly(2026, 6, 30));
        result.Payload.Lines.Should().HaveCount(2);
        result.Payload.Totals.LineCount.Should().Be(2);
        result.Payload.Totals.TotalAmountWithheld.Should().Be(750m,
            because: "10k × 5% + 2.5k × 10% = 500 + 250 = 750");
        result.Payload.Totals.TotalGrossPayment.Should().Be(12_500m);

        // Per-category roll-up.
        result.Payload.Totals.ByCategory.Should().HaveCount(2);
        result.Payload.Totals.ByCategory.Single(c => c.WhtCategoryCode == "Services")
            .AmountWithheld.Should().Be(500m);
        result.Payload.Totals.ByCategory.Single(c => c.WhtCategoryCode == "Professional")
            .AmountWithheld.Should().Be(250m);

        // Reconciliation: WHT-payable accrual matches the cert total.
        result.Payload.Reconciliation.WhtPayableAccountBalanceAtPeriodEnd.Should().Be(750m,
            because: "the two posts each credited WhtPayable for the withheld amount → 750 total");
        result.Payload.Reconciliation.MatchesTotalAmountWithheld.Should().BeTrue();
        result.Payload.Reconciliation.DiscrepancyAmount.Should().BeNull();

        // Persisted Form41Filing row.
        db.ChangeTracker.Clear();
        var filing = await db.Set<Form41Filing>().AsNoTracking()
            .FirstAsync(f => f.Id == result.Form41FilingId);
        filing.FiscalYear.Should().Be(2026);
        filing.Quarter.Should().Be(2);
        filing.Status.Should().Be(Form41Status.Unfiled);
        filing.LineCount.Should().Be(2);
        filing.TotalWhtPayable.Amount.Should().Be(750m);
    }

    [Fact]
    public async Task Generate_RefusesDuplicateForSameQuarter()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (_, user) = await SeedSharedAsync(db);

        var clock = new TestClock(new DateTime(2026, 4, 5, 10, 0, 0, DateTimeKind.Utc));
        var handler = new GenerateForm41Handler(db, clock);

        // First generate for Q1 2026 — empty quarter is fine.
        await handler.HandleAsync(new GenerateForm41Command(2026, 1, user.Id), CancellationToken.None);

        // Second attempt MUST fail.
        var act = async () => await handler.HandleAsync(
            new GenerateForm41Command(2026, 1, user.Id), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("already been generated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Generate_EmptyQuarter_ProducesValidPayloadWithZeroLines()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (_, user) = await SeedSharedAsync(db);

        var clock = new TestClock(new DateTime(2026, 4, 5, 10, 0, 0, DateTimeKind.Utc));
        var handler = new GenerateForm41Handler(db, clock);
        var result = await handler.HandleAsync(
            new GenerateForm41Command(2026, 1, user.Id), CancellationToken.None);

        result.Payload.Lines.Should().BeEmpty();
        result.Payload.Totals.LineCount.Should().Be(0);
        result.Payload.Totals.TotalAmountWithheld.Should().Be(0m);
        result.Payload.Reconciliation.MatchesTotalAmountWithheld.Should().BeTrue(
            because: "0 = 0; an empty quarter reconciles by definition");

        // Persisted row still exists — an inspector wants to see we
        // considered the quarter even when nothing was withheld.
        db.ChangeTracker.Clear();
        var filing = await db.Set<Form41Filing>().AsNoTracking()
            .FirstAsync(f => f.Id == result.Form41FilingId);
        filing.LineCount.Should().Be(0);
        filing.TotalWhtPayable.Amount.Should().Be(0m);
    }

    private static async Task PostSupplierPaymentWithWhtAsync(
        AppDbContext db, User user, VatCategory vat,
        string tinSuffix, decimal grossAmount,
        DateOnly paymentDate, string categoryCode)
    {
        // Each post needs its own supplier so the suppliers
        // dictionary in the generator is non-trivial.
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText($"مورد {tinSuffix}", $"Supplier {tinSuffix}"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse($"123456{tinSuffix}"), vat.Id));
        db.Add(supplier);

        var purchaseDraft = PurchaseInvoice.CreateDraft(supplier.Id, supplier.TaxProfile,
            $"SUP-INV-{tinSuffix}", paymentDate.AddDays(-1));
        purchaseDraft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(grossAmount),
            vatCategoryId: vat.Id, vatRatePercent: 0m,
            deductibleFlag: false);
        db.Add(purchaseDraft);
        await db.SaveChangesAsync();

        var clock = new TestClock(paymentDate.ToDateTime(TimeOnly.MinValue).AddHours(11));
        var postedPurchase = await new PostPurchaseInvoiceHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore())
            .HandleAsync(new PostPurchaseInvoiceCommand(purchaseDraft.Id, user.Id),
                CancellationToken.None);

        var voucher = SupplierPaymentVoucher.CreateDraft(supplier.Id,
            paymentDate, PaymentMethod.BankTransfer, $"PAY-{tinSuffix}",
            MoneyEgp.From(grossAmount));
        db.Add(voucher);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateSupplierPaymentCommand(voucher.Id, postedPurchase.Id, MoneyEgp.From(grossAmount)),
            CancellationToken.None);

        await new PostSupplierPaymentVoucherHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore(),
                new SupplierPaymentVoucherJournalEmitter(db),
                new SqlWhtComputeService(db))
            .HandleAsync(new PostSupplierPaymentVoucherCommand(
                voucher.Id, user.Id,
                WhtCategoryCode: categoryCode,
                WhtSourceInvoiceId: postedPurchase.Id),
                CancellationToken.None);
    }

    private static async Task<(VatCategory, User)> SeedSharedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 0m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(user);
        await db.SaveChangesAsync();
        return (vat, user);
    }

    private static async Task EnsureCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync()) return;
        db.Add(new Company(
            legalName: new ArabicEnglishText("شركة", "Test Company SAE"),
            taxRegistrationNumber: EgyptianTin.Parse("123456789"),
            commercialRegistrationNumber: "CR-1",
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "12", postalCode: "11511"),
            taxpayerActivityCode: "0001"));
        await db.SaveChangesAsync();
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public List<AuditLogPayload> Captured { get; } = [];
        public Task<AuditLogEntry> AppendAsync(AuditLogPayload payload, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(payload);
            Captured.Add(payload);
            return Task.FromResult(new AuditLogEntry(
                index: Captured.Count, tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId, actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId, kind: payload.Kind, payloadJson: payload.PayloadJson,
                prevHash: new byte[32], thisHash: new byte[32]));
        }
    }
}
