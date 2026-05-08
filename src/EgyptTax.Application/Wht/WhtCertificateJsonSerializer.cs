using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgyptTax.Application.Wht;

/// <summary>
/// FR-045 / US7 / T198 — canonical JSON serialization for
/// <see cref="WhtCertificatePayload"/>. Single source of truth so
/// the schema-validation contract test (T198), the future
/// regulator-export Form 41 generator (T209), and any inspection-
/// bundle inclusion all see byte-identical JSON shape from this
/// one place.
/// </summary>
public static class WhtCertificateJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(WhtCertificatePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Serialize(payload, Options);
    }
}
