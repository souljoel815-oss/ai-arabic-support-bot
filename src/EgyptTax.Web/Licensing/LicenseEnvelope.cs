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
// Explicit JsonPropertyName so the on-disk envelope uses lowercase
// "payload"/"signature" — matches the documented wire format above
// AND the field names InAppActivationHandler's shape-check looks up.
// Without these attributes STJ would use PascalCase ("Payload"
// /"Signature") and TryGetProperty("payload") on the parsed JSON
// would silently fail (TryGetProperty is case-sensitive), producing
// the "ليس ترخيص DaftarX صالح" error even on signature-valid tokens.
public sealed record LicenseEnvelope(
    [property: JsonPropertyName("payload")]   LicensePayload Payload,
    [property: JsonPropertyName("signature")] string Signature);

/// <summary>
/// Signed license payload. v1 had only Edition (string). v2 (Gux.13)
/// added MaxUsers + MaxCompanies + Features[] for fine-grained
/// edition gating. Pre-v2 tokens deserialize with default values for
/// the new fields; the runtime falls back to <c>Feature.DefaultsFor</c>
/// based on the parsed <see cref="LicenseEdition"/> when
/// <see cref="Features"/> is empty.
///
/// CRITICAL: <see cref="LicensePayloadSerializer.CanonicalBytes"/>
/// uses <see cref="JsonIgnoreCondition.WhenWritingDefault"/> so that
/// re-serializing a v1 payload (without the new fields set) produces
/// the same bytes that were originally signed — otherwise Ed25519
/// verification of older tokens would fail.
/// </summary>
public sealed record LicensePayload(
    [property: JsonPropertyName("version")]      int Version,
    [property: JsonPropertyName("hwid")]         string Hwid,
    [property: JsonPropertyName("customer")]     string Customer,
    [property: JsonPropertyName("edition")]      string Edition,
    [property: JsonPropertyName("issuedAtUtc")]  DateTime IssuedAtUtc,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("salesPhone")]   string SalesPhone,
    [property: JsonPropertyName("salesEmail")]   string SalesEmail,
    // --- v2 (Gux.13) additions; nullable for backward compat ---
    [property: JsonPropertyName("maxUsers"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? MaxUsers = null,
    [property: JsonPropertyName("maxCompanies"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? MaxCompanies = null,
    [property: JsonPropertyName("features"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string[]? Features = null);

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
