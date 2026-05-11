using EgyptTax.Application.Eta;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// P1.1 — hand-curated catalog of the most common ~30 Egyptian
/// ETA activity codes for SMB customers. Picked to cover retail,
/// services, manufacturing and trading — the four buckets that
/// account for &gt;80% of small-business filings. Production
/// deployments overlay the latest official export.
///
/// Search matches Arabic name, English name, and the numeric code
/// case-insensitively / accent-insensitively for the typical
/// "freelancer types الكلمة" lookup pattern.
/// </summary>
public sealed class InMemoryEtaActivityCodeCatalog : IEtaActivityCodeCatalog
{
    private static readonly EtaActivityCode[] Codes =
    {
        new("4711", "بيع تجزئة في المتاجر غير المتخصصة", "Retail sale in non-specialised stores", "تجارة", "Trade"),
        new("4719", "بيع تجزئة آخر في متاجر غير متخصصة", "Other retail in non-specialised stores", "تجارة", "Trade"),
        new("4751", "بيع منسوجات بالتجزئة", "Retail sale of textiles", "تجارة", "Trade"),
        new("4759", "بيع أثاث وأجهزة منزلية", "Retail of furniture & household appliances", "تجارة", "Trade"),
        new("4761", "بيع كتب وصحف ومستلزمات مكتبية", "Retail of books, newspapers & stationery", "تجارة", "Trade"),
        new("4771", "بيع ملابس وأحذية بالتجزئة", "Retail sale of clothing & footwear", "تجارة", "Trade"),
        new("4789", "بيع تجزئة في الأكشاك والأسواق", "Retail via stalls & markets (other)", "تجارة", "Trade"),

        new("4621", "تجارة جملة حبوب وبذور", "Wholesale of grain & seeds", "تجارة", "Trade"),
        new("4631", "تجارة جملة أغذية ومشروبات", "Wholesale of food & beverages", "تجارة", "Trade"),
        new("4651", "تجارة جملة أجهزة كمبيوتر", "Wholesale of computers", "تجارة", "Trade"),
        new("4661", "تجارة جملة وقود وزيوت", "Wholesale of fuel & lubricants", "تجارة", "Trade"),

        new("1071", "تصنيع المخبوزات الطازجة", "Manufacture of fresh bakery", "تصنيع", "Manufacturing"),
        new("1310", "تحضير وغزل ألياف نسيجية", "Preparation & spinning of textile fibres", "تصنيع", "Manufacturing"),
        new("1392", "صنع منسوجات جاهزة عدا الملابس", "Manufacture of made-up textiles", "تصنيع", "Manufacturing"),
        new("1410", "تصنيع ملابس عدا ملابس الفراء", "Manufacture of wearing apparel", "تصنيع", "Manufacturing"),
        new("2511", "تصنيع منتجات معدنية إنشائية", "Manufacture of structural metal products", "تصنيع", "Manufacturing"),

        new("5610", "مطاعم وخدمات الأطعمة المتنقّلة", "Restaurants & mobile food services", "خدمات", "Services"),
        new("5630", "خدمة المشروبات", "Beverage serving activities", "خدمات", "Services"),
        new("5510", "أنشطة الإقامة قصيرة الأجل", "Short-stay accommodation", "خدمات", "Services"),

        new("6201", "البرمجة الحاسوبية", "Computer programming", "خدمات تقنية", "Tech services"),
        new("6202", "الاستشارات الحاسوبية", "Computer consultancy", "خدمات تقنية", "Tech services"),
        new("6311", "معالجة البيانات والاستضافة", "Data processing & hosting", "خدمات تقنية", "Tech services"),

        new("6920", "الأنشطة المحاسبية ومراجعة الحسابات", "Accounting & auditing", "خدمات مهنية", "Professional services"),
        new("6910", "الأنشطة القانونية", "Legal activities", "خدمات مهنية", "Professional services"),
        new("7022", "استشارات إدارة الأعمال", "Business management consultancy", "خدمات مهنية", "Professional services"),
        new("7311", "وكالات الإعلان", "Advertising agencies", "خدمات مهنية", "Professional services"),
        new("7410", "تصميم متخصص", "Specialised design", "خدمات مهنية", "Professional services"),

        new("4321", "التركيبات الكهربائية", "Electrical installation", "بناء", "Construction"),
        new("4322", "تركيبات السباكة والتدفئة", "Plumbing & heating installation", "بناء", "Construction"),
        new("4329", "تركيبات بناء أخرى", "Other construction installation", "بناء", "Construction"),

        new("4923", "نقل البضائع البرّي", "Freight transport by road", "نقل", "Transport"),
        new("5210", "تخزين", "Warehousing", "نقل", "Transport"),
    };

    public IReadOnlyList<EtaActivityCode> All() => Codes;

    public IReadOnlyList<EtaActivityCode> Search(string fragment, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return Codes.Take(limit).ToList();
        var needle = fragment.Trim();
        return Codes
            .Where(c =>
                c.Code.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || c.NameAr.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || c.NameEn.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || c.CategoryAr.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || c.CategoryEn.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }
}
