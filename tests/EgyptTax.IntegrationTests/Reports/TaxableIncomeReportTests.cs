using EgyptTax.Application.Audit;
using EgyptTax.Application.Expenses;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Purchases;
using EgyptTax.Application.Reports;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.Infrastructure.Reports;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Reports;

/// <summary>
/// T127 / FR-023 — US2 acceptance scenario 2: a non-deductible
/// expense (e.g. personal entertainment) appears in the management
/// P&amp;L but is added back when computing taxable income, so it
/// does NOT reduce taxable profit. Verifies the arithmetic identity
/// <c>TaxableIncome = ManagementPL + NonDeductibleAdjustments</c>.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class TaxableIncomeReportTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task US2_Scenario2_NonDeductible_Expense_Is_AddedBack()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, _, item, vat, category, user) = await SeedAsync(db);

        // Revenue: 10,000 EGP sale.
        await PostSalesAsync(db, customer, item, vat, user,
            new DateOnly(2026, 3, 5), 10_000m);

        // Non-deductible expense: 500 EGP entertainment.
        await PostExpenseAsync(db, category, user,
            new DateOnly(2026, 3, 10), 500m, deductible: false);

        var report = await new SqlTaxableIncomeReportQuery(db).RunAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        report.Revenue.Amount.Should().Be(10_000m);
        report.DeductibleExpenses.Amount.Should().Be(0m);
        report.NonDeductibleAdjustments.Amount.Should().Be(500m);

        report.ManagementProfitLoss.Amount.Should().Be(9_500m,
            because: "10,000 revenue - 500 non-deductible expense = 9,500 management P&L (the internal view)");
        report.TaxableIncome.Amount.Should().Be(10_000m,
            because: "the non-deductible 500 is ADDED BACK when computing taxable profit, so taxable income equals revenue (no deductible expenses subtracted)");

        // The arithmetic identity that defines the "add-back".
        (report.ManagementProfitLoss.Amount + report.NonDeductibleAdjustments.Amount)
            .Should().Be(report.TaxableIncome.Amount,
                because: "TaxableIncome = ManagementPL + NonDeductibleAdjustments is the algebraic form of the add-back");
    }

    [Fact]
    public async Task Mixed_Deductible_And_NonDeductible_Compute_Correctly()
    {
        // Sales 10,000; deductible expense 2,000; non-deductible 800.
        // Management P&L = 10,000 - 2,000 - 800 = 7,200.
        // Taxable income = 10,000 - 2,000 = 8,000.
        // Add-back identity: 7,200 + 800 = 8,000.
        await using var db = await _fixture.CreateContextAsync();
        var (customer, supplier, item, vat, category, user) = await SeedAsync(db);

        await PostSalesAsync(db, customer, item, vat, user,
            new DateOnly(2026, 4, 1), 10_000m);
        await PostPurchaseAsync(db, supplier, vat, user,
            new DateOnly(2026, 4, 5), 2_000m, deductible: true, "SUP-A");
        await PostExpenseAsync(db, category, user,
            new DateOnly(2026, 4, 10), 800m, deductible: false);

        var report = await new SqlTaxableIncomeReportQuery(db).RunAsync(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        report.Revenue.Amount.Should().Be(10_000m);
        report.DeductibleExpenses.Amount.Should().Be(2_000m);
        report.NonDeductibleAdjustments.Amount.Should().Be(800m);
        report.ManagementProfitLoss.Amount.Should().Be(7_200m);
        report.TaxableIncome.Amount.Should().Be(8_000m);
    }

    [Fact]
    public async Task PeriodBoundaries_Are_Inclusive_OnBothEnds()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, _, item, vat, _, user) = await SeedAsync(db);

        // Posts on the very first and last day of the period — both
        // MUST count.
        await PostSalesAsync(db, customer, item, vat, user, new DateOnly(2026, 5, 1), 100m);
        await PostSalesAsync(db, customer, item, vat, user, new DateOnly(2026, 5, 31), 200m);

        var report = await new SqlTaxableIncomeReportQuery(db).RunAsync(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        report.Revenue.Amount.Should().Be(300m,
            because: "documents on PeriodStart AND PeriodEnd are both inside the inclusive window");
        report.Rows.Should().HaveCount(2);
    }

    private static async Task PostSalesAsync(AppDbContext db, Customer customer, Item item,
        VatCategory vat, User user, DateOnly date, decimal unitPrice)
    {
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, date);
        draft.AddLine(item.Id, 1m, MoneyEgp.From(unitPrice), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();
        var clock = new TestClock(date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
        var handler = new PostSalesInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), clock, new CaptureAuditLogStore());
        await handler.HandleAsync(new PostSalesInvoiceCommand(draft.Id, user.Id), CancellationToken.None);
    }

    private static async Task PostPurchaseAsync(AppDbContext db, Supplier supplier, VatCategory vat,
        User user, DateOnly date, decimal unitPrice, bool deductible, string supplierInvoiceNumber)
    {
        var draft = PurchaseInvoice.CreateDraft(supplier.Id, supplier.TaxProfile, supplierInvoiceNumber, date);
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(unitPrice),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: deductible);
        db.Add(draft);
        if (deductible)
        {
            db.Add(new Attachment(draft.Id, DocumentType.PurchaseInvoice,
                "r.pdf", $"{Guid.NewGuid():N}.pdf",
                $"attachments/2026/{date.Month:D2}/{draft.Id:D}/r.pdf",
                new byte[32], "application/pdf", 1, user.Id,
                date.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime()));
        }
        await db.SaveChangesAsync();
        var clock = new TestClock(date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
        var handler = new PostPurchaseInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), clock, new CaptureAuditLogStore());
        await handler.HandleAsync(new PostPurchaseInvoiceCommand(draft.Id, user.Id), CancellationToken.None);
    }

    private static async Task PostExpenseAsync(AppDbContext db, DeductibleExpenseCategory category,
        User user, DateOnly date, decimal amount, bool deductible)
    {
        var draft = Expense.CreateDraft(date, category.Id, MoneyEgp.From(amount),
            deductibleFlag: deductible,
            description: new ArabicEnglishText("وصف", "Description"));
        db.Add(draft);
        if (deductible)
        {
            db.Add(new Attachment(draft.Id, DocumentType.Expense,
                "r.pdf", $"{Guid.NewGuid():N}.pdf",
                $"attachments/2026/{date.Month:D2}/{draft.Id:D}/r.pdf",
                new byte[32], "application/pdf", 1, user.Id,
                date.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime()));
        }
        await db.SaveChangesAsync();
        var clock = new TestClock(date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
        var handler = new PostExpenseHandler(db,
            new SqlSequentialNumberAllocator(db), clock, new CaptureAuditLogStore());
        await handler.HandleAsync(new PostExpenseCommand(draft.Id, user.Id), CancellationToken.None);
    }

    private static async Task<(Customer customer, Supplier supplier, Item item, VatCategory vat, DeductibleExpenseCategory category, User user)>
        SeedAsync(AppDbContext db)
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
        var category = new DeductibleExpenseCategory(
            code: $"CAT-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("ترفيه", "Entertainment"),
            defaultDeductible: false,
            defaultAccountId: Guid.NewGuid());
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(customer); db.Add(supplier); db.Add(item); db.Add(category); db.Add(user);
        await db.SaveChangesAsync();
        return (customer, supplier, item, vat, category, user);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
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
