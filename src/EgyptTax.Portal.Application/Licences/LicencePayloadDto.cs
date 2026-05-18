using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgyptTax.Portal.Application.Licences;

/// <summary>
/// T028. Mirrors <c>EgyptTax.Web.Licensing.LicensePayload</c> on the
/// on-prem product byte-for-byte so the existing <c>LicenseVerifier</c>
/// accepts portal-issued tokens unchanged. CRITICAL: every JsonPropertyName
/// + the canonical-serialization options below MUST match
/// <c>src/EgyptTax.Web/Licensing/LicenseEnvelope.cs</c> EXACTLY. If those
/// drift, on-prem installs reject portal tokens with the cryptic
/// "ليس ترخيص DaftarX صالح" error.
/// </summary>
public sealed record LicencePayloadDto(
    [property: JsonPropertyName("version")]      int Version,
    [property: JsonPropertyName("hwid")]         string Hwid,
    [property: JsonPropertyName("customer")]     string Customer,
    [property: JsonPropertyName("edition")]      string Edition,
    [property: JsonPropertyName("issuedAtUtc")]  DateTime IssuedAtUtc,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("salesPhone")]   string SalesPhone,
    [property: JsonPropertyName("salesEmail")]   string SalesEmail,
    [property: JsonPropertyName("maxUsers"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? MaxUsers = null,
    [property: JsonPropertyName("maxCompanies"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? MaxCompanies = null,
    [property: JsonPropertyName("features"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string[]? Features = null);

public sealed record LicenceEnvelopeDto(
    [property: JsonPropertyName("payload")]   LicencePayloadDto Payload,
    [property: JsonPropertyName("signature")] string Signature);

/// <summary>
/// Canonical byte producer for Ed25519 signing. Output MUST match
/// <c>EgyptTax.Web.Licensing.LicensePayloadSerializer.CanonicalBytes</c>
/// byte-for-byte. JSON property order is fixed by the record's declaration
/// order — STJ honours that.
/// </summary>
public static class LicencePayloadCanonicalSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static byte[] CanonicalBytes(LicencePayloadDto payload)
        => JsonSerializer.SerializeToUtf8Bytes(payload, Options);
}
