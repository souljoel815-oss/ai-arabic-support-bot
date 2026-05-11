namespace EgyptTax.Application.Eta;

/// <summary>
/// P1.1 (ETA Wizard step 1) — port over the regulator's "Search
/// Taxpayer" endpoint. Used during onboarding to auto-fill the
/// company's legal name, address, and registered activity codes
/// from the operator's TIN, replacing the typo-prone manual entry
/// that's the #1 cause of subsequent ETA submission rejects.
///
/// Production wires a real-ETA HTTP client; the MVP wires
/// <c>MockEtaTaxpayerLookup</c> with a small in-memory roster so
/// the wizard demo is end-to-end.
/// </summary>
public interface IEtaTaxpayerLookup
{
    /// <summary>
    /// Look up an Egyptian taxpayer by TIN (9 or 14 digits).
    /// Returns <c>null</c> when the regulator has no record (TIN
    /// not registered) so the wizard can render the "register first
    /// at ETA portal" guidance instead of moving the operator past
    /// a definitely-broken setup step.
    /// </summary>
    Task<EtaTaxpayerLookupResult?> LookupByTinAsync(
        string tin,
        CancellationToken cancellationToken = default);
}

public sealed record EtaTaxpayerLookupResult(
    string Tin,
    string LegalNameAr,
    string LegalNameEn,
    string AddressAr,
    string AddressEn,
    string Governorate,
    EtaTaxpayerKind Kind,
    IReadOnlyList<string> RegisteredActivityCodes);

public enum EtaTaxpayerKind
{
    /// <summary>Registered B (legal entity) — eligible for B2B e-invoicing.</summary>
    LegalEntity,
    /// <summary>Registered I (individual / sole proprietor).</summary>
    Individual,
    /// <summary>Government body — different submission flow.</summary>
    Government,
}
