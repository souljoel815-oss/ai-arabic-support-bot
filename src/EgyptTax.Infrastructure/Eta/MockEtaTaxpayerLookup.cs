using EgyptTax.Application.Eta;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// P1.1 — in-process mock of the ETA "Search Taxpayer" endpoint
/// covering a handful of canonical fixtures plus a deterministic
/// fallback so any well-formed Egyptian TIN demos cleanly. Real
/// production wires an HTTP client against ETA's identity service.
/// </summary>
public sealed class MockEtaTaxpayerLookup : IEtaTaxpayerLookup
{
    private static readonly string[] AhramActivities = { "4711", "4719" };
    private static readonly string[] AhmedActivities = { "7022" };
    private static readonly string[] NileActivities = { "1310", "1392" };
    private static readonly string[] DefaultActivities = { "4711" };

    private static readonly Dictionary<string, EtaTaxpayerLookupResult> Fixtures = new()
    {
        ["123456789"] = new EtaTaxpayerLookupResult(
            Tin: "123456789",
            LegalNameAr: "شركة الأهرام للتجارة",
            LegalNameEn: "Al-Ahram Trading Co.",
            AddressAr: "12 شارع التحرير، الدقي، الجيزة",
            AddressEn: "12 Tahrir St., Dokki, Giza",
            Governorate: "Giza",
            Kind: EtaTaxpayerKind.LegalEntity,
            RegisteredActivityCodes: AhramActivities),

        ["987654321"] = new EtaTaxpayerLookupResult(
            Tin: "987654321",
            LegalNameAr: "أحمد محمد للاستشارات",
            LegalNameEn: "Ahmed Mohamed Consulting",
            AddressAr: "5 شارع جامعة الدول العربية، المهندسين",
            AddressEn: "5 Arab League St., Mohandessin",
            Governorate: "Giza",
            Kind: EtaTaxpayerKind.Individual,
            RegisteredActivityCodes: AhmedActivities),

        ["555000111"] = new EtaTaxpayerLookupResult(
            Tin: "555000111",
            LegalNameAr: "مصنع النيل للأقمشة",
            LegalNameEn: "Nile Textiles Manufacturing",
            AddressAr: "ك 28 طريق مصر إسكندرية الصحراوي",
            AddressEn: "Km 28 Cairo-Alex Desert Rd.",
            Governorate: "Cairo",
            Kind: EtaTaxpayerKind.LegalEntity,
            RegisteredActivityCodes: NileActivities),
    };

    public Task<EtaTaxpayerLookupResult?> LookupByTinAsync(
        string tin,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tin);

        var digits = new string(tin.Where(char.IsDigit).ToArray());
        if (digits.Length is not (9 or 14))
        {
            return Task.FromResult<EtaTaxpayerLookupResult?>(null);
        }

        if (Fixtures.TryGetValue(digits, out var fixture))
        {
            return Task.FromResult<EtaTaxpayerLookupResult?>(fixture);
        }

        // Deterministic fallback so any well-formed TIN gets a
        // realistic-looking response in dev — keeps the demo honest
        // without needing a fixtures table for every customer's TIN.
        var deterministic = new EtaTaxpayerLookupResult(
            Tin: digits,
            LegalNameAr: $"شركة عميل {digits[..Math.Min(4, digits.Length)]} للأعمال",
            LegalNameEn: $"Customer {digits[..Math.Min(4, digits.Length)]} Trading",
            AddressAr: "العنوان المسجّل لدى الضرائب",
            AddressEn: "Registered tax address",
            Governorate: "Cairo",
            Kind: EtaTaxpayerKind.LegalEntity,
            RegisteredActivityCodes: DefaultActivities);
        return Task.FromResult<EtaTaxpayerLookupResult?>(deterministic);
    }
}
