using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Onboarding;

/// <summary>
/// D0.5 (v3 roadmap) — sample-data seeder triggered from the
/// onboarding wizard's "Load sample data" button. Creates a small
/// realistic Egyptian-SMB dataset so the operator hits the
/// "first 5 minutes aha moment" without typing a single record:
///
///   • 5 Customers (mix of B2B-registered, B2C, with TIN where applicable)
///   • 8 Items (services + goods, one missing ETA code → triggers
///     the FR-035 MissingEtaCodeRule warning when posted)
///   • 3 Draft sales invoices (pre-populated lines so operator can
///     click Post and feel the speed)
///
/// Invoices land as Drafts intentionally — the user does the Post
/// click themselves, which is what surfaces the Penalty Shield
/// warning + the document-number allocation experience. That
/// 1-click "watch the system catch a missing ETA code" IS the
/// aha moment.
///
/// Idempotent: skips if any sample-marked customer already exists
/// (uses the Code prefix "SMP-" as the marker). Operator can call
/// it again safely without duplicates.
/// </summary>
public sealed class SampleDataSeeder
{
    private const string SampleCodePrefix = "SMP-";

    private readonly AppDbContext _db;

    public SampleDataSeeder(AppDbContext db) => _db = db;

    /// <summary>
    /// Adds sample data across customers, items, sales invoices,
    /// suppliers, expenses, and purchase invoices. Each kind has
    /// its own idempotency gate based on the SMP- code prefix —
    /// the operator can re-click "Load sample data" on the wizard
    /// after an upgrade adds new seed kinds without duplicating
    /// the kinds that were already added. Returns true if ANY
    /// kind was added in this call.
    /// </summary>
    public async Task<bool> SeedAsync(Guid? createdByUserId = null, CancellationToken ct = default)
    {
        var standardVat = await _db.Set<VatCategory>()
            .Where(v => v.Code == "STD-14" || v.RatePercent == 14m)
            .OrderBy(v => v.EffectiveFromDate)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Standard 14% VAT category is missing. Run the bootstrap seeder first.");

        var anyAdded = false;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ----- Customers + items + sales invoices (original kinds) -----

        var customers = await _db.Set<Customer>()
            .Where(c => c.Code.StartsWith(SampleCodePrefix))
            .ToListAsync(ct);
        if (customers.Count == 0)
        {
            customers = SeedCustomers(standardVat.Id);
            _db.AddRange(customers);
            anyAdded = true;
        }

        var items = await _db.Set<Item>()
            .Where(i => i.Code.StartsWith(SampleCodePrefix))
            .OrderBy(i => i.Code)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            items = SeedItems(standardVat.Id);
            _db.AddRange(items);
            anyAdded = true;
        }

        if (anyAdded) await _db.SaveChangesAsync(ct);

        var hasSampleInvoices = await _db.Set<SalesInvoice>()
            .AnyAsync(i => customers.Select(c => c.Id).Contains(i.CustomerId), ct);
        if (!hasSampleInvoices && customers.Count >= 3 && items.Count >= 8)
        {
            var inv1 = SalesInvoice.CreateDraft(
                customers[0].Id, customers[0].TaxProfile, today, createdByUserId);
            inv1.AddLine(items[0].Id, 2m, MoneyEgp.From(1500m), standardVat.Id, 14m);
            inv1.AddLine(items[1].Id, 5m, MoneyEgp.From(250m), standardVat.Id, 14m);

            var inv2 = SalesInvoice.CreateDraft(
                customers[1].Id, customers[1].TaxProfile, today.AddDays(-2), createdByUserId);
            inv2.AddLine(items[2].Id, 1m, MoneyEgp.From(8500m), standardVat.Id, 14m);

            // Invoice 3 deliberately uses the no-ETA-code item to
            // surface the FR-035 MissingEtaCodeRule on post — the
            // demo Penalty Shield "moat" moment.
            var inv3 = SalesInvoice.CreateDraft(
                customers[2].Id, customers[2].TaxProfile, today, createdByUserId);
            inv3.AddLine(items[7].Id, 3m, MoneyEgp.From(450m), standardVat.Id, 14m);

            _db.AddRange(inv1, inv2, inv3);
            anyAdded = true;
        }

        // ----- Suppliers (added per audit recommendation) -----

        var suppliers = await _db.Set<Supplier>()
            .Where(s => s.Code.StartsWith(SampleCodePrefix))
            .ToListAsync(ct);
        if (suppliers.Count == 0)
        {
            suppliers = SeedSuppliers(standardVat.Id);
            _db.AddRange(suppliers);
            anyAdded = true;
        }

        if (anyAdded) await _db.SaveChangesAsync(ct);

        // ----- Expenses (3 drafts across categories) -----

        var hasSampleExpenses = await _db.Set<Expense>()
            .AnyAsync(e => e.Description.English.StartsWith("[sample]"), ct);
        if (!hasSampleExpenses)
        {
            var expenseCats = await _db.Set<DeductibleExpenseCategory>()
                .Where(c => c.Status == DeductibleExpenseCategoryStatus.Active)
                .OrderBy(c => c.Code)
                .Take(3)
                .ToListAsync(ct);
            if (expenseCats.Count > 0)
            {
                _db.AddRange(SeedExpenses(expenseCats, today));
                anyAdded = true;
            }
        }

        // ----- Purchase invoices (2 drafts) -----

        var hasSamplePurchases = await _db.Set<PurchaseInvoice>()
            .AnyAsync(p => p.SupplierInvoiceNumber.StartsWith("SMP-"), ct);
        if (!hasSamplePurchases && suppliers.Count >= 2 && items.Count >= 4)
        {
            _db.AddRange(SeedPurchaseInvoices(suppliers, items, standardVat.Id, today));
            anyAdded = true;
        }

        if (anyAdded) await _db.SaveChangesAsync(ct);
        return anyAdded;
    }

    private static List<Supplier> SeedSuppliers(Guid standardVatId)
    {
        var addrCairo = new ArabicEnglishText("القاهرة، مصر الجديدة", "Cairo, Heliopolis");
        var addrAlex = new ArabicEnglishText("الإسكندرية، سيدي بشر", "Alexandria, Sidi Bishr");

        return new List<Supplier>
        {
            new(
                code: $"{SampleCodePrefix}SUP-001",
                name: new ArabicEnglishText("شركة الدلتا للتوريدات", "Delta Supplies Co."),
                address: addrCairo,
                taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                    EgyptianTin.Parse("200300400"), defaultPurchaseVatCategoryId: standardVatId),
                phone: "+201001112222",
                email: "sales@deltasupplies.example.eg"),
            new(
                code: $"{SampleCodePrefix}SUP-002",
                name: new ArabicEnglishText("الشركة المصرية للأجهزة المكتبية", "Egyptian Office Equipment"),
                address: addrCairo,
                taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                    EgyptianTin.Parse("500600700"), defaultPurchaseVatCategoryId: standardVatId),
                phone: "+201112223333",
                email: "info@egyptoffice.example.eg"),
            new(
                code: $"{SampleCodePrefix}SUP-003",
                name: new ArabicEnglishText("مطبعة الإسكندرية الحديثة", "Modern Alexandria Press"),
                address: addrAlex,
                taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                    EgyptianTin.Parse("800900100"), defaultPurchaseVatCategoryId: standardVatId),
                phone: "+201223334444",
                email: "orders@modernpress.example.eg"),
            new(
                code: $"{SampleCodePrefix}SUP-004",
                name: new ArabicEnglishText("محمد الفنّان (سباك)", "Mohamed Al-Fanan (plumber)"),
                address: addrCairo,
                taxProfile: SupplierTaxProfile.Unregistered(defaultPurchaseVatCategoryId: standardVatId),
                phone: "+201234445555"),
            new(
                code: $"{SampleCodePrefix}SUP-005",
                name: new ArabicEnglishText("Cloudflare Inc.", "Cloudflare Inc."),
                address: new ArabicEnglishText("سان فرانسيسكو، أمريكا", "San Francisco, USA"),
                taxProfile: SupplierTaxProfile.ForeignSupplier(defaultPurchaseVatCategoryId: standardVatId),
                email: "billing@cloudflare.example"),
        };
    }

    private static List<Expense> SeedExpenses(List<DeductibleExpenseCategory> cats, DateOnly today)
    {
        // The "[sample]" prefix in the English description is the
        // idempotency marker — checked above so re-runs skip.
        var expenses = new List<Expense>
        {
            Expense.CreateDraft(
                documentDate: today.AddDays(-3),
                categoryId: cats[0].Id,
                amount: MoneyEgp.From(450m),
                deductibleFlag: true,
                description: new ArabicEnglishText(
                    "[عينة] فاتورة تليفون شهر مارس",
                    "[sample] March phone bill")),
        };
        if (cats.Count >= 2)
        {
            expenses.Add(Expense.CreateDraft(
                documentDate: today.AddDays(-7),
                categoryId: cats[1].Id,
                amount: MoneyEgp.From(1200m),
                deductibleFlag: true,
                description: new ArabicEnglishText(
                    "[عينة] إيجار مكتب شهري",
                    "[sample] Monthly office rent share")));
        }
        if (cats.Count >= 3)
        {
            expenses.Add(Expense.CreateDraft(
                documentDate: today.AddDays(-1),
                categoryId: cats[2].Id,
                amount: MoneyEgp.From(280m),
                deductibleFlag: true,
                description: new ArabicEnglishText(
                    "[عينة] غداء مع عميل",
                    "[sample] Client lunch")));
        }
        return expenses;
    }

    private static List<PurchaseInvoice> SeedPurchaseInvoices(
        List<Supplier> suppliers, List<Item> items, Guid standardVatId, DateOnly today)
    {
        // Two drafts so /purchase-invoices isn't empty on the demo
        // tenant. Both reference the seeded suppliers + items so the
        // master-data lookup populates fields cleanly. Idempotency
        // marker = SMP- prefix on SupplierInvoiceNumber.
        var p1 = PurchaseInvoice.CreateDraft(
            supplierId: suppliers[0].Id,
            supplierTaxProfileSnapshot: suppliers[0].TaxProfile,
            supplierInvoiceNumber: "SMP-INV-A001",
            dateReceived: today.AddDays(-5));
        p1.AddLine(
            itemId: items[0].Id, expenseCategoryId: null,
            quantity: 10m, unitPrice: MoneyEgp.From(1200m),
            vatCategoryId: standardVatId, vatRatePercent: 14m,
            deductibleFlag: true);

        var p2 = PurchaseInvoice.CreateDraft(
            supplierId: suppliers[1].Id,
            supplierTaxProfileSnapshot: suppliers[1].TaxProfile,
            supplierInvoiceNumber: "SMP-INV-B042",
            dateReceived: today.AddDays(-2));
        p2.AddLine(
            itemId: items[3].Id, expenseCategoryId: null,
            quantity: 1m, unitPrice: MoneyEgp.From(2400m),
            vatCategoryId: standardVatId, vatRatePercent: 14m,
            deductibleFlag: true);

        return new List<PurchaseInvoice> { p1, p2 };
    }

    private static List<Customer> SeedCustomers(Guid standardVatId)
    {
        var addrCairo = PostalAddress.Create(
            new ArabicEnglishText("القاهرة، المعادي", "Cairo, Maadi"),
            governorate: "Cairo", regionCity: "Maadi", street: "Street 9", buildingNumber: "12");
        var addrAlex = PostalAddress.Create(
            new ArabicEnglishText("الإسكندرية، سموحة", "Alexandria, Smouha"),
            governorate: "Alexandria", regionCity: "Smouha", street: "Victor Emanuel", buildingNumber: "55");
        var addrGiza = PostalAddress.Create(
            new ArabicEnglishText("الجيزة، الدقي", "Giza, Dokki"),
            governorate: "Giza", regionCity: "Dokki", street: "Tahrir", buildingNumber: "8");

        return new List<Customer>
        {
            new(
                code: $"{SampleCodePrefix}001",
                name: new ArabicEnglishText("شركة النيل للتجارة", "Nile Trading Co."),
                address: addrCairo,
                taxProfile: CustomerTaxProfile.B2BRegistered(
                    EgyptianTin.Parse("100200300"), vatExemption: false, defaultSalesVatCategoryId: standardVatId),
                phone: "+201001234567",
                email: "ar@niletrading.example.eg"),
            new(
                code: $"{SampleCodePrefix}002",
                name: new ArabicEnglishText("مجموعة الأهرام للاستشارات", "Pyramid Consulting Group"),
                address: addrCairo,
                taxProfile: CustomerTaxProfile.B2BRegistered(
                    EgyptianTin.Parse("400500600"), vatExemption: false, defaultSalesVatCategoryId: standardVatId),
                phone: "+201112345678",
                email: "billing@pyramidconsulting.example.eg"),
            new(
                code: $"{SampleCodePrefix}003",
                name: new ArabicEnglishText("مكتبة الإسكندرية للتوريدات", "Alexandria Supplies Bookstore"),
                address: addrAlex,
                taxProfile: CustomerTaxProfile.B2BRegistered(
                    EgyptianTin.Parse("700800900"), vatExemption: false, defaultSalesVatCategoryId: standardVatId),
                phone: "+201223456789",
                email: "orders@alexsupplies.example.eg"),
            new(
                code: $"{SampleCodePrefix}004",
                name: new ArabicEnglishText("محمد عبد الرحمن", "Mohamed AbdelRahman"),
                address: addrGiza,
                taxProfile: CustomerTaxProfile.B2CConsumer(
                    vatExemption: false, defaultSalesVatCategoryId: standardVatId),
                phone: "+201001234500"),
            new(
                code: $"{SampleCodePrefix}005",
                name: new ArabicEnglishText("منيرة فهمي", "Mounira Fahmy"),
                address: addrGiza,
                taxProfile: CustomerTaxProfile.B2CConsumer(
                    vatExemption: false, defaultSalesVatCategoryId: standardVatId),
                phone: "+201112345600"),
        };
    }

    private static List<Item> SeedItems(Guid standardVatId)
    {
        // Items 1-7 carry sample EGS codes so they post cleanly.
        // Item 8 (last in list) has NO ETA code — when used in
        // an invoice and posted, the FR-035 MissingEtaCodeRule
        // fires. This is the intentional demo trigger.
        return new List<Item>
        {
            new(code: $"{SampleCodePrefix}SVC-001",
                name: new ArabicEnglishText("استشارات محاسبية / ساعة", "Accounting consultancy / hour"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: "EG-1100100"),
            new(code: $"{SampleCodePrefix}SVC-002",
                name: new ArabicEnglishText("تدريب موظفين / يوم", "Staff training / day"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: "EG-1100200"),
            new(code: $"{SampleCodePrefix}HW-001",
                name: new ArabicEnglishText("لاب توب 14 بوصة", "14-inch laptop"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: "EG-2200100"),
            new(code: $"{SampleCodePrefix}HW-002",
                name: new ArabicEnglishText("شاشة 27 بوصة", "27-inch monitor"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: "EG-2200200"),
            new(code: $"{SampleCodePrefix}OFC-001",
                name: new ArabicEnglishText("ورق طباعة A4 (رزمة)", "A4 print paper (ream)"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: "EG-3300100"),
            new(code: $"{SampleCodePrefix}OFC-002",
                name: new ArabicEnglishText("حبر طابعة (خرطوشة)", "Printer ink (cartridge)"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: "EG-3300200"),
            new(code: $"{SampleCodePrefix}SUB-001",
                name: new ArabicEnglishText("اشتراك سحابي شهري", "Monthly cloud subscription"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: "EG-4400100"),
            new(code: $"{SampleCodePrefix}DEMO-PENALTY",
                name: new ArabicEnglishText("صنف بدون كود ETA (لتجربة Penalty Shield)", "Item without ETA code (Penalty Shield demo)"),
                defaultVatCategoryId: standardVatId,
                etaItemCode: null),
        };
    }
}
