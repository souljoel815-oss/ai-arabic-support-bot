using EgyptTax.Domain.MasterData;
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

        return added;
    }
}
