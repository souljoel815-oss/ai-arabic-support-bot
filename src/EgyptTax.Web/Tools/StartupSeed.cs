using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Numbering;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Web.Tools;

/// <summary>
/// Idempotent startup seeder for "must-exist" master data (VAT
/// categories today; chart of accounts + tax periods can join here as
/// they get carved out of the install-time Seeder). Runs on every
/// service start so existing installs catch up automatically without
/// re-running the MSI.
/// </summary>
internal static class StartupSeed
{
    public static async Task<int> EnsureDefaultsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Set<VatCategory>()
            .Select(v => v.Code)
            .ToListAsync(ct);

        var defaults = new[]
        {
            new VatCategory(
                code: "STANDARD-14",
                name: new ArabicEnglishText("القياسي 14%", "Standard 14%"),
                ratePercent: 14m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: true),
            new VatCategory(
                code: "REDUCED-5",
                name: new ArabicEnglishText("المخفّض 5% (آلات ومعدات)", "Reduced 5% (machinery)"),
                ratePercent: 5m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: true),
            new VatCategory(
                code: "ZERO-RATED",
                name: new ArabicEnglishText("صفرية (تصدير)", "Zero-rated (export)"),
                ratePercent: 0m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: true),
            new VatCategory(
                code: "EXEMPT",
                name: new ArabicEnglishText("معفاة", "Exempt"),
                ratePercent: 0m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: false),
        };

        var added = 0;
        foreach (var cat in defaults)
        {
            if (!existing.Contains(cat.Code))
            {
                db.Add(cat);
                added++;
            }
        }
        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        // Standard role catalogue. ADMIN is seeded by the install-
        // time Seeder; the rest are added here so existing installs
        // catch up automatically. MFA defaults follow the FR-001
        // intent: elevated roles (ADMIN, ACCOUNTANT, MANAGER) get
        // requires-MFA = true so the operator can flip it on without
        // re-seeding; user-facing roles (SALES_REP, CASHIER, AUDITOR)
        // default off. The operator can override per-role via Settings.
        var seededRoles = await db.Set<Role>()
            .Select(r => r.Code)
            .ToListAsync(ct);
        var defaultRoles = new[]
        {
            (Code: "MANAGER",    NameAr: "مدير",            NameEn: "Manager",                Mfa: true),
            (Code: "ACCOUNTANT", NameAr: "محاسب",           NameEn: "Accountant",             Mfa: true),
            (Code: "BOOKKEEPER", NameAr: "مساعد محاسب",     NameEn: "Bookkeeper",             Mfa: false),
            (Code: "CASHIER",    NameAr: "أمين الخزينة",    NameEn: "Cashier",                Mfa: false),
            (Code: "SALES_REP",  NameAr: "مندوب مبيعات",    NameEn: "Sales Representative",   Mfa: false),
            (Code: "AUDITOR",    NameAr: "مراجع",           NameEn: "Auditor (read-only)",    Mfa: false),
        };
        var rolesAdded = 0;
        foreach (var r in defaultRoles)
        {
            if (!seededRoles.Contains(r.Code))
            {
                db.Add(new Role(r.Code, new ArabicEnglishText(r.NameAr, r.NameEn), r.Mfa));
                rolesAdded++;
            }
        }
        if (rolesAdded > 0)
        {
            await db.SaveChangesAsync(ct);
            added += rolesAdded;
        }

        // BUG-004 (May 2026 testing report) — DocumentSeries is seeded
        // by migration 20260507105423_NumberingCore in full-mode (SQL
        // Server) installs, but portable-mode boots use EnsureCreated
        // and never run migrations, so the table was empty and the
        // first invoice Post threw "No DocumentSeries seeded for
        // document type SalesInvoice." Idempotent re-seed here covers
        // both paths. GUIDs match the migration so cross-mode
        // references stay stable.
        var existingSeriesTypes = await db.Set<DocumentSeries>()
            .Select(s => s.DocumentType)
            .ToListAsync(ct);
        var defaultSeries = new[]
        {
            (Id: new Guid("11111111-1111-4111-8111-000000000001"), Code: "INV", NameAr: "فاتورة مبيعات",       NameEn: "Sales invoice",            Type: DocumentType.SalesInvoice),
            (Id: new Guid("11111111-1111-4111-8111-000000000002"), Code: "CN",  NameAr: "إشعار خصم",           NameEn: "Credit note",              Type: DocumentType.CreditNote),
            (Id: new Guid("11111111-1111-4111-8111-000000000003"), Code: "PI",  NameAr: "فاتورة مشتريات",      NameEn: "Purchase invoice",         Type: DocumentType.PurchaseInvoice),
            (Id: new Guid("11111111-1111-4111-8111-000000000004"), Code: "EXP", NameAr: "مصروف",               NameEn: "Expense",                  Type: DocumentType.Expense),
            (Id: new Guid("11111111-1111-4111-8111-000000000005"), Code: "JV",  NameAr: "قيد محاسبي",          NameEn: "Journal voucher",          Type: DocumentType.JournalVoucher),
            (Id: new Guid("11111111-1111-4111-8111-000000000006"), Code: "SPV", NameAr: "إيصال دفع لمورد",     NameEn: "Supplier payment voucher", Type: DocumentType.SupplierPaymentVoucher),
            (Id: new Guid("11111111-1111-4111-8111-000000000007"), Code: "CRV", NameAr: "إيصال قبض من عميل",   NameEn: "Customer receipt voucher", Type: DocumentType.CustomerReceiptVoucher),
            (Id: new Guid("11111111-1111-4111-8111-000000000008"), Code: "FA",  NameAr: "أصل ثابت",            NameEn: "Fixed asset",              Type: DocumentType.FixedAsset),
        };
        var seriesAdded = 0;
        foreach (var s in defaultSeries)
        {
            if (!existingSeriesTypes.Contains(s.Type))
            {
                db.Add(new DocumentSeries(s.Id, s.Code, new ArabicEnglishText(s.NameAr, s.NameEn), s.Type));
                seriesAdded++;
            }
        }
        if (seriesAdded > 0)
        {
            await db.SaveChangesAsync(ct);
            added += seriesAdded;
        }

        // P3.1 — ensure a default cash account exists so SPV/CRV
        // posts have something to anchor the cash leg to.
        if (!await db.Set<CashAccount>().AnyAsync(ct))
        {
            db.Add(new CashAccount(
                accountCode: "1100.00",
                name: new ArabicEnglishText("الخزينة الرئيسية", "Main Cash"),
                kind: CashAccountKind.Cash,
                currency: "EGP",
                isDefault: true));
            await db.SaveChangesAsync(ct);
            added++;
        }

        // L4 (v3 roadmap, phase 1) — ensure a default stock location
        // exists. Without one, items have no place to record per-
        // location stock when the operator starts using multi-warehouse
        // features. Auto-creates "MAIN" → "المخزن الرئيسي" / "Main
        // Warehouse" if no locations exist at all. Operator can rename
        // it from /stock-locations.
        if (!await db.Set<StockLocation>().AnyAsync(ct))
        {
            db.Add(new StockLocation(
                code: "MAIN",
                name: new ArabicEnglishText("المخزن الرئيسي", "Main Warehouse"),
                isDefault: true));
            await db.SaveChangesAsync(ct);
            added++;
        }

        // v3 §11 #7 (multi-currency) — ensure EGP exists as the
        // base currency. Other currencies are operator-added.
        if (!await db.Set<EgyptTax.Domain.Settings.Currency>().AnyAsync(ct))
        {
            db.Add(new EgyptTax.Domain.Settings.Currency(
                code: "EGP", symbol: "ج.م",
                nameEn: "Egyptian Pound", nameAr: "الجنيه المصري",
                isBase: true));
            await db.SaveChangesAsync(ct);
            added++;
        }

        return added;
    }
}
