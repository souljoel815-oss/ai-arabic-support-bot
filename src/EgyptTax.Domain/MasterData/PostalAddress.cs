using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// Structured Egyptian postal address. The PDF renderer (T079) shows
/// the bilingual <see cref="DisplayArabic"/>/<see cref="DisplayEnglish"/>
/// pair; the ETA eInvoice generator (T077) consumes the structured
/// fields per the address shape required by
/// <c>contracts/eta-einvoice.schema.json#/$defs/address</c>
/// (country/governate/regionCity/street/buildingNumber + optional
/// postalCode). Country is fixed to <c>EG</c> for the single-country
/// MVP. Display is flattened to two strings rather than nesting
/// <see cref="ArabicEnglishText"/> because EF8 ComplexProperty cannot
/// bind a nested record-struct constructor when the outer is also a
/// record struct.
/// </summary>
public readonly record struct PostalAddress(
    string DisplayArabic,
    string DisplayEnglish,
    string Country,
    string Governorate,
    string RegionCity,
    string Street,
    string BuildingNumber,
    string? PostalCode
)
{
    /// <summary>
    /// Convenience accessor for callers that already work with
    /// <see cref="ArabicEnglishText"/> (PDF renderer, mostly).
    /// </summary>
    public ArabicEnglishText Display => new(DisplayArabic, DisplayEnglish);

    public static PostalAddress Create(
        ArabicEnglishText display,
        string governorate,
        string regionCity,
        string street,
        string buildingNumber,
        string? postalCode = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(governorate);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionCity);
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildingNumber);

        return new PostalAddress(
            DisplayArabic: display.Arabic,
            DisplayEnglish: display.English,
            Country: "EG",
            Governorate: governorate,
            RegionCity: regionCity,
            Street: street,
            BuildingNumber: buildingNumber,
            PostalCode: postalCode
        );
    }
}
