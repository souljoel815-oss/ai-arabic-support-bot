using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
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
    /// Returns true if sample data was added; false if it was
    /// already present (idempotent skip).
    /// </summary>
    public async Task<bool> SeedAsync(Guid? createdByUserId = null, CancellationToken ct = default)
    {
        var alreadySeeded = await _db.Set<Customer>()
            .AnyAsync(c => c.Code.StartsWith(SampleCodePrefix), ct);
        if (alreadySeeded) return false;

        var standardVat = await _db.Set<VatCategory>()
            .Where(v => v.Code == "STD-14" || v.RatePercent == 14m)
            .OrderBy(v => v.EffectiveFromDate)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Standard 14% VAT category is missing. Run the bootstrap seeder first.");

        var customers = SeedCustomers(standardVat.Id);
        var items = SeedItems(standardVat.Id);

        _db.AddRange(customers);
        _db.AddRange(items);
        await _db.SaveChangesAsync(ct);

        // Three Draft invoices using the seeded customers + items.
        // Lines pre-priced so totals look realistic on the list view.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inv1 = SalesInvoice.CreateDraft(
            customers[0].Id,
            customers[0].TaxProfile,
            today,
            createdByUserId);
        inv1.AddLine(items[0].Id, quantity: 2m, MoneyEgp.From(1500m), standardVat.Id, vatRatePercent: 14m);
        inv1.AddLine(items[1].Id, quantity: 5m, MoneyEgp.From(250m),  standardVat.Id, vatRatePercent: 14m);

        var inv2 = SalesInvoice.CreateDraft(
            customers[1].Id,
            customers[1].TaxProfile,
            today.AddDays(-2),
            createdByUserId);
        inv2.AddLine(items[2].Id, quantity: 1m, MoneyEgp.From(8500m), standardVat.Id, vatRatePercent: 14m);

        // Invoice 3 deliberately uses the no-ETA-code item so that
        // when the operator hits Post they see the MissingEtaCodeRule
        // warning fire — this is the demo "moat" moment.
        var inv3 = SalesInvoice.CreateDraft(
            customers[2].Id,
            customers[2].TaxProfile,
            today,
            createdByUserId);
        inv3.AddLine(items[7].Id, quantity: 3m, MoneyEgp.From(450m), standardVat.Id, vatRatePercent: 14m);

        _db.AddRange(inv1, inv2, inv3);
        await _db.SaveChangesAsync(ct);

        return true;
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
