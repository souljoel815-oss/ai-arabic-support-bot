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
/// T196 / SC-011 — the WHT pipeline accuracy gate. Posts 50
/// supplier-services payments across Q2 2026 at varying WHT rates
/// (Services 5%, Professional 10%, Royalties 20%) with assorted
/// gross amounts that exercise the rounding edges, then asserts:
///
///   * Form 41 reconciliation matches to the cent (cert total =
///     WHT-payable accrual; the spec allows ±1 EGP tolerance, but
///     the auto-emitter math is deterministic so we expect exact).
///   * Form 41 line count = 50 (zero omission — every cert is
///     included).
///   * No duplicate certs in the filing (zero dup — assert by
///     UNIQUE certificate-number set inside the lines).
///   * Dashboard's Owed view reconciles to the ledger's
///     WhtPayable accrual.
///   * Per-category roll-up sums match the per-rate cert count.
///
/// This is the perf+accuracy bedrock for the WHT lifecycle —
/// matches what T228 did for US9.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class WhtComputationAccuracyTests(SqlServerFixture fixture)
{
    private const int InvoicesPerQuarter = 50;
    private const int RandomSeed = 1996; // matches the task number for reproducibility

    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task SC011_FiftyPaymentsAtVaryingRates_FormFortyOneReconciles_AndDashboardMatches()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (vat, user) = await SeedSharedAsync(db);

        // Seed three WHT categories at distinct rates.
        var services5 = new WhtCategory(
            code: "Services",
            name: new ArabicEnglishText("خدمات", "Services"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices
        );
        var professional10 = new WhtCategory(
            code: "Professional",
            name: new ArabicEnglishText("مهني", "Professional"),
            ratePercent: 10m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices
        );
        var royalties20 = new WhtCategory(
            code: "Royalties",
            name: new ArabicEnglishText("إتاوات", "Royalties"),
            ratePercent: 20m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices
        );
        db.Add(services5);
        db.Add(professional10);
        db.Add(royalties20);
        await db.SaveChangesAsync();

        // Post 50 supplier payments with WHT splits across Q2 2026.
        // Vary rates round-robin and amounts via a fixed-seed RNG so
        // the run is deterministic but exercises rounding paths.
        var rng = new Random(RandomSeed);
        var categoryCodes = new[] { "Services", "Professional", "Royalties" };
        var perCategoryExpected = new Dictionary<string, decimal>
        {
            ["Services"] = 0m,
            ["Professional"] = 0m,
            ["Royalties"] = 0m,
        };
        var perCategoryCount = new Dictionary<string, int>
        {
            ["Services"] = 0,
            ["Professional"] = 0,
            ["Royalties"] = 0,
        };
        decimal totalGross = 0m;
        decimal totalWithheld = 0m;

        for (var i = 0; i < InvoicesPerQuarter; i++)
        {
            // Random gross [500, 25_000] with arbitrary cents to
            // exercise the F2-rounding path.
            var gross = decimal.Round(
                500m + (decimal)(rng.NextDouble() * 24_500),
                2,
                MidpointRounding.ToEven
            );
            // Spread across 91 days of Q2.
            var paymentDate = new DateOnly(2026, 4, 1).AddDays(i % 91);
            var categoryCode = categoryCodes[i % categoryCodes.Length];
            var rate = categoryCode switch
            {
                "Services" => 5m,
                "Professional" => 10m,
                "Royalties" => 20m,
                _ => throw new InvalidOperationException(),
            };

            await PostSupplierPaymentWithWhtAsync(
                db,
                user,
                vat,
                tinSuffix: $"{700 + i:D3}",
                grossAmount: gross,
                paymentDate: paymentDate,
                categoryCode: categoryCode
            );

            // Track the expected withheld amount (same rounding the
            // compute service uses).
            var expectedWht = decimal.Round(gross * rate / 100m, 2, MidpointRounding.ToEven);
            totalGross += gross;
            totalWithheld += expectedWht;
            perCategoryExpected[categoryCode] += expectedWht;
            perCategoryCount[categoryCode]++;

            // Clear tracker every 10 posts to keep memory tame.
            if (i % 10 == 9)
                db.ChangeTracker.Clear();
        }

        // Generate Form 41 for Q2.
        var clock = new TestClock(new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc));
        var generate = new GenerateForm41Handler(db, clock);
        var result = await generate.HandleAsync(
            new GenerateForm41Command(2026, 2, user.Id),
            CancellationToken.None
        );

        // Zero omission: every cert appears exactly once.
        result
            .Payload.Lines.Should()
            .HaveCount(
                InvoicesPerQuarter,
                because: $"SC-011 — Form 41 MUST include all {InvoicesPerQuarter} certs from the quarter (zero omission)"
            );

        // Zero duplication: cert numbers MUST be unique.
        var certNumbers = result.Payload.Lines.Select(l => l.OutboundCertificateNumber).ToList();
        certNumbers
            .Distinct()
            .Count()
            .Should()
            .Be(
                InvoicesPerQuarter,
                because: "SC-011 — every cert appears exactly once in the filing (zero duplication)"
            );

        // Reconciliation: cert total = WHT-payable accrual.
        result
            .Payload.Reconciliation.MatchesTotalAmountWithheld.Should()
            .BeTrue(
                because: $"SC-011 — reconciliation MUST match for a clean filing; discrepancy = {result.Payload.Reconciliation.DiscrepancyAmount:F2}"
            );
        result
            .Payload.Totals.TotalAmountWithheld.Should()
            .Be(
                totalWithheld,
                because: "the sum of cert AmountWithheld values MUST equal the running total computed during seeding"
            );

        // Per-category roll-up matches per-rate counts + totals.
        foreach (var category in categoryCodes)
        {
            var rollUp = result.Payload.Totals.ByCategory.SingleOrDefault(c =>
                c.WhtCategoryCode == category
            );
            rollUp.Should().NotBeNull(because: $"category {category} should appear in the roll-up");
            rollUp!.LineCount.Should().Be(perCategoryCount[category]);
            rollUp.AmountWithheld.Should().Be(perCategoryExpected[category]);
        }

        // Dashboard owed view reconciles to the cert total (since
        // the filing isn't yet Filed, certs are still "owed").
        var dashboard = await new SqlWhtLifecycleDashboardQuery(db).GetAsync(
            asOf: new DateOnly(2026, 7, 5)
        );
        dashboard.Owed.CertCount.Should().Be(InvoicesPerQuarter);
        dashboard
            .Owed.TotalAccruedNotYetFiled.Should()
            .Be(
                totalWithheld,
                because: "owed view aggregates the same certs Form 41 sees — they MUST agree to the cent"
            );

        // Total gross across the quarter equals the sum we tracked.
        result.Payload.Totals.TotalGrossPayment.Should().Be(totalGross);
    }

    private static async Task PostSupplierPaymentWithWhtAsync(
        AppDbContext db,
        User user,
        VatCategory vat,
        string tinSuffix,
        decimal grossAmount,
        DateOnly paymentDate,
        string categoryCode
    )
    {
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText($"مورد {tinSuffix}", $"Supplier {tinSuffix}"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                EgyptianTin.Parse($"123456{tinSuffix}"),
                vat.Id
            )
        );
        db.Add(supplier);

        var purchaseDraft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            $"SUP-INV-{tinSuffix}",
            paymentDate.AddDays(-1)
        );
        purchaseDraft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(grossAmount),
            vatCategoryId: vat.Id,
            vatRatePercent: 0m,
            deductibleFlag: false
        );
        db.Add(purchaseDraft);
        await db.SaveChangesAsync();

        var clock = new TestClock(paymentDate.ToDateTime(TimeOnly.MinValue).AddHours(11));
        var postedPurchase = await new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore()
        ).HandleAsync(
            new PostPurchaseInvoiceCommand(purchaseDraft.Id, user.Id),
            CancellationToken.None
        );

        var voucher = SupplierPaymentVoucher.CreateDraft(
            supplier.Id,
            paymentDate,
            PaymentMethod.BankTransfer,
            $"PAY-{tinSuffix}",
            MoneyEgp.From(grossAmount)
        );
        db.Add(voucher);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateSupplierPaymentCommand(
                voucher.Id,
                postedPurchase.Id,
                MoneyEgp.From(grossAmount)
            ),
            CancellationToken.None
        );

        await new PostSupplierPaymentVoucherHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore(),
            new SupplierPaymentVoucherJournalEmitter(db),
            new SqlWhtComputeService(db)
        ).HandleAsync(
            new PostSupplierPaymentVoucherCommand(
                voucher.Id,
                user.Id,
                WhtCategoryCode: categoryCode,
                WhtSourceInvoiceId: postedPurchase.Id
            ),
            CancellationToken.None
        );
    }

    private static async Task<(VatCategory, User)> SeedSharedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 0m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(vat);
        db.Add(user);
        await db.SaveChangesAsync();
        return (vat, user);
    }

    private static async Task EnsureCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync())
            return;
        db.Add(
            new Company(
                legalName: new ArabicEnglishText("شركة", "Test Company SAE"),
                taxRegistrationNumber: EgyptianTin.Parse("123456789"),
                commercialRegistrationNumber: "CR-1",
                address: PostalAddress.Create(
                    new ArabicEnglishText("القاهرة", "Cairo"),
                    "Cairo",
                    "Downtown",
                    "Tahrir",
                    "12",
                    postalCode: "11511"
                ),
                taxpayerActivityCode: "0001"
            )
        );
        await db.SaveChangesAsync();
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public List<AuditLogPayload> Captured { get; } = [];

        public Task<AuditLogEntry> AppendAsync(
            AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            Captured.Add(payload);
            return Task.FromResult(
                new AuditLogEntry(
                    index: Captured.Count,
                    tsUtc: DateTime.UtcNow,
                    actorUserId: payload.ActorUserId,
                    actorFirmName: payload.ActorFirmName,
                    companyId: payload.CompanyId,
                    kind: payload.Kind,
                    payloadJson: payload.PayloadJson,
                    prevHash: new byte[32],
                    thisHash: new byte[32]
                )
            );
        }
    }
}
