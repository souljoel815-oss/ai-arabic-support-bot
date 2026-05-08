using System.Diagnostics;
using EgyptTax.Application.Reports;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Reports;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Reports;

/// <summary>
/// T240 / SC-002 — VAT-monthly + Taxable-income report each render
/// in under 5 s p95 at 5,000 documents under 25 concurrent users.
///
/// Seeds 5,000 posted documents (3,000 sales + 1,500 purchases +
/// 500 expenses) directly via aggregate APIs (CreateDraft +
/// AddLine + MarkPosted) — bypasses the post-handler pipeline so
/// the seed completes in tens of seconds rather than minutes. The
/// posted state + line totals are produced by the same
/// <c>Recompute</c> path the handler would invoke, so the read
/// queries see realistic data.
///
/// Then runs both reports concurrently from 25 separate
/// <see cref="AppDbContext"/> instances against the same DB
/// (mirrors the SC-002 25-user load) and asserts:
///   - p95 of every report run completes under 5 s,
///   - max latency stays under 10 s (a degenerate 99th-percentile
///     guard so a long tail can't slip past a tight p95).
///
/// Slow test — ~30 s seed + ~30 s concurrent runs at 5k scale —
/// so [Trait("Category","Slow")] keeps it out of the default
/// developer loop and into nightly CI.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Slow")]
public class ReportPerformanceTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    private const int SalesCount = 3_000;
    private const int PurchaseCount = 1_500;
    private const int ExpenseCount = 500;
    private const int ConcurrentRunners = 25;
    private const int TotalDocs = SalesCount + PurchaseCount + ExpenseCount;

    [Fact]
    public async Task Sc002_BothReports_RenderUnder5sP95_At5kDocs_25ConcurrentUsers()
    {
        await using var seedDb = await _fixture.CreateContextAsync();
        var connectionString = seedDb.Database.GetConnectionString()!;
        var fixtureData = await SeedAsync(seedDb);

        var seedSw = Stopwatch.StartNew();
        await BulkSeedDocumentsAsync(seedDb, fixtureData);
        seedSw.Stop();

        // Sanity — confirm the seed produced what we expected.
        var postedSales = await seedDb
            .Set<SalesInvoice>()
            .AsNoTracking()
            .CountAsync(s => s.State == DocumentState.Posted);
        var postedPurchases = await seedDb
            .Set<PurchaseInvoice>()
            .AsNoTracking()
            .CountAsync(p => p.State == DocumentState.Posted);
        var postedExpenses = await seedDb
            .Set<Expense>()
            .AsNoTracking()
            .CountAsync(e => e.State == DocumentState.Posted);
        postedSales.Should().Be(SalesCount);
        postedPurchases.Should().Be(PurchaseCount);
        postedExpenses.Should().Be(ExpenseCount);

        // 25 concurrent runners each open their own AppDbContext +
        // run BOTH reports back-to-back, capturing per-call latency.
        var runDurations = new System.Collections.Concurrent.ConcurrentBag<TimeSpan>();
        var maxLatency = TimeSpan.Zero;
        var maxLock = new object();

        await Task.WhenAll(
            Enumerable
                .Range(0, ConcurrentRunners)
                .Select(async _ =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlServer(connectionString)
                        .Options;
                    await using var ctx = new AppDbContext(options);

                    var vatSw = Stopwatch.StartNew();
                    var vatReport = await new SqlVatMonthlyReportQuery(ctx).RunAsync(2026, 6);
                    vatSw.Stop();
                    runDurations.Add(vatSw.Elapsed);
                    UpdateMax(vatSw.Elapsed);

                    var tiSw = Stopwatch.StartNew();
                    var tiReport = await new SqlTaxableIncomeReportQuery(ctx).RunAsync(
                        new DateOnly(2026, 6, 1),
                        new DateOnly(2026, 6, 30)
                    );
                    tiSw.Stop();
                    runDurations.Add(tiSw.Elapsed);
                    UpdateMax(tiSw.Elapsed);

                    // Sanity-check non-empty results so a degenerate fast query
                    // (e.g. a regression that returns empty rows quickly) can't
                    // pass the perf gate by accident.
                    vatReport.Rows.Count.Should().BeGreaterThan(0);
                    tiReport.Rows.Count.Should().BeGreaterThan(0);

                    void UpdateMax(TimeSpan d)
                    {
                        lock (maxLock)
                        {
                            if (d > maxLatency)
                                maxLatency = d;
                        }
                    }
                })
        );

        var sorted = runDurations.OrderBy(d => d).ToList();
        sorted
            .Count.Should()
            .Be(
                ConcurrentRunners * 2,
                because: "each runner produces 2 latency samples (VAT + Taxable Income)"
            );

        var p95Index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
        var p95 = sorted[Math.Clamp(p95Index, 0, sorted.Count - 1)];
        var median = sorted[sorted.Count / 2];

        p95.Should()
            .BeLessThan(
                TimeSpan.FromSeconds(5),
                because: $"SC-002 — p95 MUST land under 5 s at {TotalDocs} docs / {ConcurrentRunners} concurrent users; "
                    + $"actual p95 = {p95.TotalMilliseconds:F0} ms (median {median.TotalMilliseconds:F0} ms, "
                    + $"max {maxLatency.TotalMilliseconds:F0} ms, seed {seedSw.Elapsed.TotalSeconds:F1} s)"
            );

        maxLatency
            .Should()
            .BeLessThan(
                TimeSpan.FromSeconds(10),
                because: "long-tail guard — even a single 10 s+ outlier degrades the operator UX past acceptable"
            );
    }

    private static async Task BulkSeedDocumentsAsync(AppDbContext db, FixtureData f)
    {
        // Disable change-detection during the bulk seed — at 5k docs +
        // 5k+ lines, repeated DetectChanges() dominates the wall clock.
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        const int BatchSize = 500;
        var sequence = 0;

        for (var i = 0; i < SalesCount; i++)
        {
            var date = new DateOnly(2026, 6, 1).AddDays(i % 30);
            var draft = SalesInvoice.CreateDraft(f.Customer.Id, f.Customer.TaxProfile, date);
            draft.AddLine(
                f.Item.Id,
                1m,
                MoneyEgp.From(1_000m + (i % 500)),
                f.Vat.Id,
                f.Vat.RatePercent
            );
            draft.MarkPosted(
                documentNumber: $"INV-2026-{++sequence:D6}",
                postedByUserId: f.User.Id,
                postedAtUtc: date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime(),
                postingMode: DocumentPostingMode.UnapprovedDirect,
                approvalEnabled: false
            );
            db.Add(draft);

            if ((i + 1) % BatchSize == 0)
            {
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        for (var i = 0; i < PurchaseCount; i++)
        {
            var date = new DateOnly(2026, 6, 1).AddDays(i % 30);
            var draft = PurchaseInvoice.CreateDraft(
                f.Supplier.Id,
                f.Supplier.TaxProfile,
                $"SUP-{i:D6}",
                date
            );
            // Half deductible, half non-deductible.
            var deductible = (i % 2) == 0;
            draft.AddLine(
                itemId: null,
                expenseCategoryId: f.ExpenseCategory.Id,
                quantity: 1m,
                unitPrice: MoneyEgp.From(500m + (i % 300)),
                vatCategoryId: f.Vat.Id,
                vatRatePercent: f.Vat.RatePercent,
                deductibleFlag: deductible
            );
            draft.MarkPosted(
                documentNumber: $"PUR-2026-{++sequence:D6}",
                postedByUserId: f.User.Id,
                postedAtUtc: date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime(),
                postingMode: DocumentPostingMode.UnapprovedDirect,
                approvalEnabled: false
            );
            db.Add(draft);

            if ((i + 1) % BatchSize == 0)
            {
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        for (var i = 0; i < ExpenseCount; i++)
        {
            var date = new DateOnly(2026, 6, 1).AddDays(i % 30);
            var deductible = (i % 2) == 0;
            var draft = Expense.CreateDraft(
                date,
                f.ExpenseCategory.Id,
                MoneyEgp.From(100m + (i % 200)),
                deductibleFlag: deductible,
                description: new ArabicEnglishText("وصف", "Desc")
            );
            draft.MarkPosted(
                documentNumber: $"EXP-2026-{++sequence:D6}",
                postedByUserId: f.User.Id,
                postedAtUtc: date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime(),
                postingMode: DocumentPostingMode.UnapprovedDirect,
                approvalEnabled: false
            );
            db.Add(draft);

            if ((i + 1) % BatchSize == 0)
            {
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }

    private static async Task<FixtureData> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        var customer = new Customer(
            code: $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo",
                "Downtown",
                "Tahrir",
                "1"
            ),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                false,
                vat.Id
            )
        );
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                EgyptianTin.Parse("123456789"),
                vat.Id
            )
        );
        var item = new Item(
            code: $"ITEM-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("صنف", "Item"),
            defaultVatCategoryId: vat.Id
        );
        var category = new DeductibleExpenseCategory(
            code: $"CAT-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("فئة", "Category"),
            defaultDeductible: true,
            defaultAccountId: Guid.NewGuid()
        );
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(vat);
        db.Add(customer);
        db.Add(supplier);
        db.Add(item);
        db.Add(category);
        db.Add(user);
        await db.SaveChangesAsync();
        return new FixtureData(customer, supplier, item, category, vat, user);
    }

    private sealed record FixtureData(
        Customer Customer,
        Supplier Supplier,
        Item Item,
        DeductibleExpenseCategory ExpenseCategory,
        VatCategory Vat,
        User User
    );
}
