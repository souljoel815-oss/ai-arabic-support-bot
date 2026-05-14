using System.Globalization;
using EgyptTax.Domain.Crm;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Onboarding;

/// <summary>
/// D2.5 (v3 roadmap) — per-row import results returned by the
/// CSV import handlers. Lists every row that succeeded + every row
/// that failed with a reason. The UI shows the success count + a
/// per-row error report.
/// </summary>
public sealed record ImportResult(
    int TotalRowsRead,
    int InsertedCount,
    int SkippedCount,
    IReadOnlyList<ImportRowError> Errors);

public sealed record ImportRowError(int RowNumber, string Code, string Reason);

/// <summary>
/// D2.5 — Customer CSV import. Expected columns (case-insensitive,
/// Arabic synonyms accepted):
///
///   Required: Code (or "كود"), NameEnglish (or "Name (English)"),
///             NameArabic (or "Name (Arabic)" or "الاسم")
///   Optional: Tin, Email, Phone, Governorate, RegionCity, Street,
///             BuildingNumber, CreditLimitEgp
///
/// Behaviour:
///   - Existing customer with same Code → SKIPPED (not overwritten)
///   - Tin present → creates B2BRegistered profile
///   - Tin missing → creates B2CConsumer profile
///   - Address fields default to "—" when blank (PostalAddress
///     rejects empty required fields)
///
/// Idempotent: re-importing the same file produces 0 inserts +
/// SKIPPED reports. Operator can fix errors in source + re-upload.
/// </summary>
public sealed class CustomerImportHandler
{
    private readonly AppDbContext _db;

    public CustomerImportHandler(AppDbContext db) => _db = db;

    public async Task<ImportResult> ImportAsync(
        IReadOnlyList<Dictionary<string, string>> rows,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var existingCodes = await _db.Set<Customer>()
            .AsNoTracking()
            .Select(c => c.Code)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var defaultVatId = await _db.Set<VatCategory>()
            .Where(v => v.RatePercent == 14m)
            .OrderBy(v => v.EffectiveFromDate)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(ct);

        var errors = new List<ImportRowError>();
        var inserted = 0;
        var skipped = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            // Row 1 in user-visible numbering = first data row (header was row 0).
            var rowNumber = i + 2;
            string code = "";
            try
            {
                code = CsvImporter.Required(row, "Code", "Code", "كود", "code");

                if (existingSet.Contains(code))
                {
                    skipped++;
                    errors.Add(new ImportRowError(rowNumber, code,
                        "Skipped: customer with this code already exists."));
                    continue;
                }

                var nameEn = CsvImporter.Required(row, "NameEnglish",
                    "NameEnglish", "Name (English)", "Name English", "english_name");
                var nameAr = CsvImporter.Required(row, "NameArabic",
                    "NameArabic", "Name (Arabic)", "الاسم", "arabic_name");

                var tin = CsvImporter.Get(row, "Tin", "TIN", "الرقم الضريبي");
                var email = CsvImporter.Get(row, "Email", "البريد", "email");
                var phone = CsvImporter.Get(row, "Phone", "الهاتف", "phone");

                var govCol = CsvImporter.Get(row, "Governorate", "المحافظة") ?? "—";
                var regionCol = CsvImporter.Get(row, "RegionCity", "Region", "المدينة") ?? "—";
                var streetCol = CsvImporter.Get(row, "Street", "الشارع") ?? "—";
                var bldgCol = CsvImporter.Get(row, "BuildingNumber", "Building", "رقم العقار") ?? "—";

                var address = PostalAddress.Create(
                    display: new ArabicEnglishText(nameAr, nameEn),
                    governorate: govCol,
                    regionCity: regionCol,
                    street: streetCol,
                    buildingNumber: bldgCol);

                CustomerTaxProfile profile;
                if (!string.IsNullOrWhiteSpace(tin))
                {
                    // Throws on invalid TIN; the catch below records as a per-row error.
                    profile = CustomerTaxProfile.B2BRegistered(
                        EgyptianTin.Parse(tin), vatExemption: false, defaultSalesVatCategoryId: defaultVatId);
                }
                else
                {
                    profile = CustomerTaxProfile.B2CConsumer(
                        vatExemption: false, defaultSalesVatCategoryId: defaultVatId);
                }

                var customer = new Customer(
                    code: code,
                    name: new ArabicEnglishText(nameAr, nameEn),
                    address: address,
                    taxProfile: profile,
                    phone: phone,
                    email: email);

                var creditRaw = CsvImporter.Get(row, "CreditLimitEgp", "Credit Limit", "حد الائتمان");
                var credit = CsvImporter.TryParseDecimal(creditRaw);
                if (credit is { } limit)
                {
                    customer.UpdateCreditLimit(limit);
                }

                _db.Add(customer);
                existingSet.Add(code);
                inserted++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(rowNumber, code, ex.Message));
            }
        }

        if (inserted > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new ImportResult(
            TotalRowsRead: rows.Count,
            InsertedCount: inserted,
            SkippedCount: skipped,
            Errors: errors);
    }
}

/// <summary>
/// D2.5 — Item CSV import. Expected columns:
///
///   Required: Code, NameEnglish, NameArabic
///   Optional: EtaItemCode (warning if missing — Penalty Shield
///             will flag posted invoices that reference items
///             without an ETA code)
///
/// All items default to the standard 14% VAT category. Operators
/// who need a non-standard category set it manually after import.
/// </summary>
public sealed class ItemImportHandler
{
    private readonly AppDbContext _db;

    public ItemImportHandler(AppDbContext db) => _db = db;

    public async Task<ImportResult> ImportAsync(
        IReadOnlyList<Dictionary<string, string>> rows,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var existingCodes = await _db.Set<Item>()
            .AsNoTracking()
            .Select(i => i.Code)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var defaultVatId = await _db.Set<VatCategory>()
            .Where(v => v.RatePercent == 14m)
            .OrderBy(v => v.EffectiveFromDate)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(ct);
        if (defaultVatId is null)
        {
            throw new InvalidOperationException(
                "No 14% VAT category exists. Run the bootstrap seeder before importing items.");
        }

        var errors = new List<ImportRowError>();
        var inserted = 0;
        var skipped = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 2;
            string code = "";
            try
            {
                code = CsvImporter.Required(row, "Code", "Code", "كود", "code");
                if (existingSet.Contains(code))
                {
                    skipped++;
                    errors.Add(new ImportRowError(rowNumber, code,
                        "Skipped: item with this code already exists."));
                    continue;
                }

                var nameEn = CsvImporter.Required(row, "NameEnglish",
                    "NameEnglish", "Name (English)", "english_name");
                var nameAr = CsvImporter.Required(row, "NameArabic",
                    "NameArabic", "Name (Arabic)", "الاسم", "arabic_name");
                var etaCode = CsvImporter.Get(row, "EtaItemCode", "ETA Item Code", "كود ETA");

                var item = new Item(
                    code: code,
                    name: new ArabicEnglishText(nameAr, nameEn),
                    defaultVatCategoryId: defaultVatId.Value,
                    etaItemCode: etaCode);
                _db.Add(item);
                existingSet.Add(code);
                inserted++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(rowNumber, code, ex.Message));
            }
        }

        if (inserted > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new ImportResult(
            TotalRowsRead: rows.Count,
            InsertedCount: inserted,
            SkippedCount: skipped,
            Errors: errors);
    }
}

/// <summary>
/// v4 B.1 — Lead CSV import. Sales reps coming from a spreadsheet
/// of leads need to bulk-load them into the kanban without typing
/// 50 rows by hand. Required columns: Name, Phone OR Email
/// (at least one). Optional: Company, Source, Stage,
/// ExpectedValueEgp, ExpectedCloseDate.
///
/// De-duplication: existing leads matched by Phone OR Email are
/// SKIPPED (re-import is safe).
/// </summary>
public sealed class LeadImportHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public LeadImportHandler(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ImportResult> ImportAsync(
        IReadOnlyList<Dictionary<string, string>> rows,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var existingContacts = await _db.Set<Lead>()
            .AsNoTracking()
            .Select(l => new { l.Phone, l.Email })
            .ToListAsync(ct);
        var existingPhones = existingContacts
            .Where(c => !string.IsNullOrWhiteSpace(c.Phone))
            .Select(c => c.Phone!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmails = existingContacts
            .Where(c => !string.IsNullOrWhiteSpace(c.Email))
            .Select(c => c.Email!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<ImportRowError>();
        var inserted = 0;
        var skipped = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 2;
            string nameLabel = "";
            try
            {
                var name = CsvImporter.Required(row, "Name",
                    "Name", "الاسم", "FullName", "Lead Name");
                nameLabel = name;

                var phone = CsvImporter.Get(row, "Phone", "الهاتف", "phone", "Mobile");
                var email = CsvImporter.Get(row, "Email", "البريد", "email");

                if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
                {
                    throw new InvalidOperationException(
                        "Lead requires at least one of Phone or Email.");
                }

                if (!string.IsNullOrWhiteSpace(phone) && existingPhones.Contains(phone.Trim()))
                {
                    skipped++;
                    errors.Add(new ImportRowError(rowNumber, name,
                        "Skipped: a lead with this phone already exists."));
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(email) && existingEmails.Contains(email.Trim()))
                {
                    skipped++;
                    errors.Add(new ImportRowError(rowNumber, name,
                        "Skipped: a lead with this email already exists."));
                    continue;
                }

                var company = CsvImporter.Get(row, "Company", "CompanyName", "الشركة");
                var source = CsvImporter.Get(row, "Source", "المصدر", "Lead Source");

                var lead = Lead.Create(
                    name: new ArabicEnglishText(name, name),
                    companyName: company,
                    phone: phone,
                    email: email,
                    source: source,
                    assignedToUserId: null,
                    createdAtUtc: _clock.UtcNow,
                    createdByUserId: null);

                var stageRaw = CsvImporter.Get(row, "Stage", "Status", "المرحلة");
                if (!string.IsNullOrWhiteSpace(stageRaw)
                    && Enum.TryParse<LeadStage>(stageRaw.Trim(), ignoreCase: true, out var stage)
                    && stage is not LeadStage.New)
                {
                    if (stage == LeadStage.Lost)
                    {
                        lead.MarkLost(reason: "Imported as Lost.", nowUtc: _clock.UtcNow);
                    }
                    else
                    {
                        lead.MoveToStage(stage);
                    }
                }

                var valueRaw = CsvImporter.Get(row, "ExpectedValueEgp", "Value", "Deal Value", "قيمة الصفقة");
                var dateRaw = CsvImporter.Get(row, "ExpectedCloseDate", "Close Date", "تاريخ الإغلاق المتوقع");
                var value = CsvImporter.TryParseDecimal(valueRaw);
                DateOnly? closeDate = null;
                if (!string.IsNullOrWhiteSpace(dateRaw)
                    && DateOnly.TryParse(dateRaw, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var d))
                {
                    closeDate = d;
                }
                if (value is not null || closeDate is not null)
                {
                    lead.SetForecast(closeDate, value);
                }

                _db.Add(lead);
                if (!string.IsNullOrWhiteSpace(phone)) existingPhones.Add(phone.Trim());
                if (!string.IsNullOrWhiteSpace(email)) existingEmails.Add(email.Trim());
                inserted++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(rowNumber, nameLabel, ex.Message));
            }
        }

        if (inserted > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new ImportResult(
            TotalRowsRead: rows.Count,
            InsertedCount: inserted,
            SkippedCount: skipped,
            Errors: errors);
    }
}
