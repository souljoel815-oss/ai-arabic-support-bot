using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgyptTax.Application.Wht;

/// <summary>
/// FR-046 / US7 / T199 — canonical JSON serialization for
/// <see cref="Form41Payload"/>. Same shape conventions as the WHT
/// certificate serializer (camelCase + UnsafeRelaxed encoder +
/// WhenWritingNull omit + JsonStringEnumConverter). Single source
/// of truth so the schema-validation contract test, the regulator
/// export, and any inspection-bundle inclusion all see byte-
/// identical bytes.
/// </summary>
public static class Form41JsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(Form41Payload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Serialize(payload, Options);
    }
}
