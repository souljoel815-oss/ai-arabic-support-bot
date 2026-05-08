using System.IO.Compression;
using System.Text;
using EgyptTax.Application.Expenses;
using EgyptTax.Application.Inspection;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.FileStorage;
using EgyptTax.Infrastructure.Inspection;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.Infrastructure.Reports;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Inspection;

/// <summary>
/// US9 / T233 — every inspection bundle MUST carry the five
/// register PDFs (sales-invoice, purchase+expense, credit-note,
/// general-journal, trial-balance) at the documented relative
/// paths with the schema-mandated category strings, and each PDF
/// MUST be a real PDF file (starts with the %PDF magic header).
/// Without this, the inspector arrives at an "empty" bundle and
/// the FR-048 promise of "drop the ZIP, read the registers" fails.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BundleRegisterPdfsTests(SqlServerFixture fixture) : IDisposable
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(), $"egypttax-bundle-registers-{Guid.NewGuid():N}");

    [Fact]
    public async Task Bundle_Carries_All_Five_Register_Pdfs_AtDocumentedPaths_AndCategories()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);

        // Seed enough state to populate every register with at least
        // one row: 1 customer + 1 supplier + 1 vat category + 1
        // expense category + 1 user; then post 1 sales invoice + 1
        // credit note + 1 purchase invoice + 1 expense. That gives
        // each of the five registers a real row to render.
        var (vat, customer, supplier, expenseCategory, user) = await SeedMastersAsync(db);
        var clock = new TestClock(new DateTime(2026, 5, 15, 11, 0, 0, DateTimeKind.Utc));
        var auditStore = new SqlAuditLogStore(db);
        var allocator = new SqlSequentialNumberAllocator(db);

        var salesDraft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile,
            new DateOnly(2026, 5, 5));
        salesDraft.AddLine(itemId: Guid.NewGuid(), quantity: 1m,
            unitPrice: MoneyEgp.From(200m), vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent);
        db.Add(salesDraft);
        await db.SaveChangesAsync();

        var emitter = new SalesInvoiceJournalEmitter(db);
        var postSales = new PostSalesInvoiceHandler(db, allocator, clock, auditStore, emitter);
        await postSales.HandleAsync(new PostSalesInvoiceCommand(salesDraft.Id, user.Id), CancellationToken.None);

        // Reload posted sales invoice + create credit note via the handler.
        var postedSales = await db.Set<SalesInvoice>().AsNoTracking()
            .FirstAsync(i => i.Id == salesDraft.Id);
        var cnDraft = SalesInvoice.CreateCreditNoteFor(postedSales,
            reason: "Goods returned by customer", documentDate: new DateOnly(2026, 5, 12));
        cnDraft.AddLine(itemId: Guid.NewGuid(), quantity: -1m,
            unitPrice: MoneyEgp.From(200m), vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent);
        db.Add(cnDraft);
        await db.SaveChangesAsync();
        await postSales.HandleAsync(new PostSalesInvoiceCommand(cnDraft.Id, user.Id), CancellationToken.None);

        // Purchase invoice — no attachment for simplicity.
        var purchaseDraft = PurchaseInvoice.CreateDraft(supplier.Id, supplier.TaxProfile,
            "SUP-INV-T233", new DateOnly(2026, 5, 8));
        // Non-deductible to skip the FR-016 attachment-required guard
        // (we just need a posted purchase to exist for the register).
        purchaseDraft.AddLine(itemId: null, expenseCategoryId: expenseCategory.Id,
            quantity: 1m, unitPrice: MoneyEgp.From(150m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: false);
        db.Add(purchaseDraft);
        await db.SaveChangesAsync();
        var postPurchase = new PostPurchaseInvoiceHandler(db, allocator, clock, auditStore);
        await postPurchase.HandleAsync(new PostPurchaseInvoiceCommand(purchaseDraft.Id, user.Id), CancellationToken.None);

        // Expense — non-deductible so it doesn't trigger the
        // attachment-required guard.
        var expense = Expense.CreateDraft(
            documentDate: new DateOnly(2026, 5, 10),
            categoryId: expenseCategory.Id,
            amount: MoneyEgp.From(75m),
            deductibleFlag: false,
            description: new ArabicEnglishText("مصاريف عامة", "Office supplies"));
        db.Add(expense);
        await db.SaveChangesAsync();
        var postExpense = new PostExpenseHandler(db, allocator, clock, auditStore);
        await postExpense.HandleAsync(
            new PostExpenseCommand(expense.Id, user.Id), CancellationToken.None);

        // Build the bundle.
        var store = new FileSystemAttachmentStore(_tempRoot, clock);
        var builder = new InspectionBundleBuilder(db, store, clock,
            new SqlTrialBalanceReportQuery(db));
        var result = await builder.BuildAsync(new InspectionBundleRequest(
            PeriodStart: new DateOnly(2026, 5, 1),
            PeriodEnd: new DateOnly(2026, 5, 31),
            GeneratedByUserId: user.Id,
            AllowDrafts: false), CancellationToken.None);

        // Manifest sanity: every register MUST appear at the
        // documented path + with the schema-mandated category.
        var registerExpectations = new (string Path, string Category)[]
        {
            ("registers/sales-invoice-register.pdf",          "SalesInvoiceRegister"),
            ("registers/purchase-and-expense-register.pdf",   "PurchaseInvoiceAndExpenseRegister"),
            ("registers/credit-note-and-reversal-register.pdf", "CreditNoteAndReversalRegister"),
            ("registers/general-journal-listing.pdf",         "GeneralJournalListing"),
            ("registers/trial-balance.pdf",                   "TrialBalance"),
        };
        foreach (var (path, category) in registerExpectations)
        {
            result.Manifest.Files.Should().Contain(
                f => f.RelativePath == path && f.Category == category,
                because: $"the bundle MUST carry {path} ({category}) — that's the FR-048 promise the inspector reads first");
        }

        // Each PDF in the ZIP MUST start with the %PDF magic header
        // — a quick but powerful sanity check that QuestPDF rendered
        // a real PDF rather than (e.g.) an empty byte array because
        // of a swallowed exception.
        await using var zipStream = new MemoryStream(result.ZipBytes);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var pdfMagic = Encoding.ASCII.GetBytes("%PDF");

        foreach (var (path, _) in registerExpectations)
        {
            var entry = zip.GetEntry(path);
            entry.Should().NotBeNull(because: $"{path} must be present in the ZIP");
            await using var s = entry!.Open();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            var bytes = ms.ToArray();
            bytes.Length.Should().BeGreaterThan(500,
                because: $"{path} should be a real PDF, not an empty / stub file");
            bytes.Take(4).Should().Equal(pdfMagic,
                because: $"{path} must start with the %PDF magic header");
        }
    }

    private static async Task<(VatCategory, Customer, Supplier, DeductibleExpenseCategory, User)> SeedMastersAsync(
        AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var customer = new Customer(
            code: $"CUS-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer LLC"),
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "10", postalCode: "11511"),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                vatExemption: false,
                defaultSalesVatCategoryId: null));
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("123456789"), vat.Id));
        var expenseCategory = new DeductibleExpenseCategory(
            code: $"EC-{Guid.NewGuid():N}".Substring(0, 8),
            name: new ArabicEnglishText("مصاريف عامة", "Office supplies"),
            defaultDeductible: false,
            defaultAccountId: Guid.NewGuid());
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(customer); db.Add(supplier);
        db.Add(expenseCategory); db.Add(user);
        await db.SaveChangesAsync();
        return (vat, customer, supplier, expenseCategory, user);
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

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
