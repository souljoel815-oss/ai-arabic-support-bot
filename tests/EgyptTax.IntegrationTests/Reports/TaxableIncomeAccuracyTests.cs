using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Expenses;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.Infrastructure.Reports;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.IntegrationTests.Reports;

/// <summary>
/// T241 / SC-005 / FR-023 — taxable-income accuracy at scale. Posts
/// 200 sales invoices + 200 purchase invoices with deterministic
/// pseudo-random amounts (mix of deductible / non-deductible) and
/// asserts the report's Revenue / DeductibleExpenses /
/// NonDeductibleAdjustments / TaxableIncome match the manual
/// running-total control within ±1 EGP (the rounding tolerance the
/// success criterion explicitly allows for piaster-level drift).
///
/// The point of this test is to catch silent aggregation drift at
/// scale that small-N tests miss — e.g. if the SQL aggregation ever
/// switched from a SUM over decimal to a SUM over float, the
/// last-digit drift would compound across hundreds of rows and
/// blow past the 1-EGP tolerance.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class TaxableIncomeAccuracyTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Sc005_TwoHundredSalesPlusTwoHundredPurchases_MatchesManualControl_WithinOneEgp()
    {
        await using var db = await _fixture.CreateContextAsync();
        var fixtureData = await SeedAsync(db);

        // Deterministic seed so failures are reproducible.
        var rng = new Random(8675309);
        var allocator = new SqlSequentialNumberAllocator(db);

        // Track running totals manually as we seed.
        var controlRevenue = 0m;
        var controlDeductibleExpenses = 0m;
        var controlNonDeductibleAdjustments = 0m;

        // 200 sales — amounts in [100, 9_999.99] EGP, 2 decimal piaster precision.
        for (var i = 0; i < 200; i++)
        {
            var amount = Math.Round((decimal)(rng.NextDouble() * 9_900) + 100m, 2);
            controlRevenue += amount;

            var date = new DateOnly(2026, 6, 1).AddDays(rng.Next(0, 30));
            var draft = SalesInvoice.CreateDraft(
                fixtureData.Customer.Id, fixtureData.Customer.TaxProfile, date);
            draft.AddLine(fixtureData.Item.Id, 1m, MoneyEgp.From(amount),
                fixtureData.Vat.Id, fixtureData.Vat.RatePercent);
            db.Add(draft);
            await db.SaveChangesAsync();

            var clock = new TestClock(date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
            await new PostSalesInvoiceHandler(db, allocator, clock, new NoOpAuditLogStore())
                .HandleAsync(new PostSalesInvoiceCommand(draft.Id, fixtureData.User.Id), CancellationToken.None);
        }

        // 200 purchase invoices — half deductible, half non-deductible,
        // amounts in [50, 4_999.99] EGP.
        for (var i = 0; i < 200; i++)
        {
            var amount = Math.Round((decimal)(rng.NextDouble() * 4_950) + 50m, 2);
            var deductible = i % 2 == 0;
            if (deductible)
            {
                controlDeductibleExpenses += amount;
            }
            else
            {
                controlNonDeductibleAdjustments += amount;
            }

            var date = new DateOnly(2026, 6, 1).AddDays(rng.Next(0, 30));
            var draft = PurchaseInvoice.CreateDraft(fixtureData.Supplier.Id, fixtureData.Supplier.TaxProfile,
                $"SUP-{i.ToString(CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}".Substring(0, 24), date);
            draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
                quantity: 1m, unitPrice: MoneyEgp.From(amount),
                vatCategoryId: fixtureData.Vat.Id, vatRatePercent: fixtureData.Vat.RatePercent,
                deductibleFlag: deductible);
            db.Add(draft);
            if (deductible)
            {
                db.Add(new Attachment(draft.Id, DocumentType.PurchaseInvoice,
                    "r.pdf", $"{Guid.NewGuid():N}.pdf",
                    $"attachments/2026/06/{draft.Id:D}/r.pdf",
                    new byte[32], "application/pdf", 1, fixtureData.User.Id,
                    date.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime()));
            }
            await db.SaveChangesAsync();

            var clock = new TestClock(date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
            await new PostPurchaseInvoiceHandler(db, allocator, clock, new NoOpAuditLogStore())
                .HandleAsync(new PostPurchaseInvoiceCommand(draft.Id, fixtureData.User.Id), CancellationToken.None);
        }

        var report = await new SqlTaxableIncomeReportQuery(db).RunAsync(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        // SC-005 — ±1 EGP tolerance on every aggregate.
        report.Revenue.Amount.Should().BeApproximately(controlRevenue, 1m,
            because: "SC-005 — Revenue MUST match the manual control within ±1 EGP at 200 sales scale");
        report.DeductibleExpenses.Amount.Should().BeApproximately(controlDeductibleExpenses, 1m,
            because: "SC-005 — DeductibleExpenses MUST match within ±1 EGP at 100 deductible-purchase scale");
        report.NonDeductibleAdjustments.Amount.Should().BeApproximately(controlNonDeductibleAdjustments, 1m,
            because: "SC-005 — NonDeductibleAdjustments MUST match within ±1 EGP at 100 non-deductible-purchase scale");
        report.TaxableIncome.Amount.Should().BeApproximately(
            controlRevenue - controlDeductibleExpenses, 1m,
            because: "SC-005 — TaxableIncome (Revenue - DeductibleExpenses) MUST match within ±1 EGP");

        // Add-back identity MUST hold exactly (no rounding involved —
        // it's a pure subtraction of running totals).
        (report.ManagementProfitLoss.Amount + report.NonDeductibleAdjustments.Amount)
            .Should().Be(report.TaxableIncome.Amount,
                because: "the add-back identity TaxableIncome = ManagementPL + NonDeductibleAdjustments must always hold exactly");

        report.Rows.Should().HaveCount(400,
            because: "200 sales contribute Revenue rows + 100 deductible purchases + 100 non-deductible purchases = 400");
    }

    private static async Task<FixtureData> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var customer = new Customer(
            code: $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "1"),
            taxProfile: CustomerTaxProfile.B2BRegistered(EgyptianTin.Parse("987654321"), false, vat.Id));
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("123456789"), vat.Id));
        var item = new Item(
            code: $"ITEM-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("صنف", "Item"),
            defaultVatCategoryId: vat.Id);
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(customer); db.Add(supplier); db.Add(item); db.Add(user);
        await db.SaveChangesAsync();
        return new FixtureData(customer, supplier, item, vat, user);
    }

    private sealed record FixtureData(
        Customer Customer, Supplier Supplier, Item Item, VatCategory Vat, User User);

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class NoOpAuditLogStore : IAuditLogStore
    {
        public Task<AuditLogEntry> AppendAsync(AuditLogPayload payload, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(payload);
            return Task.FromResult(new AuditLogEntry(
                index: 1, tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId, actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId, kind: payload.Kind, payloadJson: payload.PayloadJson,
                prevHash: new byte[32], thisHash: new byte[32]));
        }
    }
}
