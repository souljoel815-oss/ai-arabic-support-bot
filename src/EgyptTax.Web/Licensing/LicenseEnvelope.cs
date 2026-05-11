using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgyptTax.Web.Licensing;

/// <summary>
/// Vendor-issued, Ed25519-signed license token. The vendor's
/// EgyptTax.Web.exe license-issue verb produces these; the customer's
/// install verifies them against the hardcoded public key in
/// <see cref="LicensePublicKey"/>.
///
/// Wire format (UTF-8 JSON):
/// {
///   "payload": {
///     "version": 1,
///     "hwid": "ABCD-1234-EF56-7890",
///     "customer": "شركة الأمل",
///     "edition": "Standard",
///     "issuedAtUtc": "2026-05-11T10:00:00Z",
///     "expiresAtUtc": "2027-05-11T10:00:00Z",
///     "salesPhone": "+20 100 123 4567",
///     "salesEmail": "sales@daftarx.local"
///   },
///   "signature": "<base64 Ed25519 sig over the canonical JSON of payload>"
/// }
/// </summary>
public sealed record LicenseEnvelope(LicensePayload Payload, string Signature);

public sealed record LicensePayload(
    [property: JsonPropertyName("version")]      int Version,
    [property: JsonPropertyName("hwid")]         string Hwid,
    [property: JsonPropertyName("customer")]     string Customer,
    [property: JsonPropertyName("edition")]      string Edition,
    [property: JsonPropertyName("issuedAtUtc")]  DateTime IssuedAtUtc,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("salesPhone")]   string SalesPhone,
    [property: JsonPropertyName("salesEmail")]   string SalesEmail);

/// <summary>
/// Stable serializer used by both the issuer (signing side) and the
/// verifier (customer side). MUST produce identical bytes for the
/// same payload — Ed25519 signatures are byte-exact.
/// </summary>
public static class LicensePayloadSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // No PropertyNamingPolicy — JsonPropertyName attributes are
        // explicit. WriteIndented MUST be false; we sign the
        // canonical bytes.
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static byte[] CanonicalBytes(LicensePayload payload)
        => JsonSerializer.SerializeToUtf8Bytes(payload, Options);
}
