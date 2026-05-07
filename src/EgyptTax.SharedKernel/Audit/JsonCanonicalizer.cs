using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EgyptTax.SharedKernel.Audit;

/// <summary>
/// JSON Canonicalization Scheme (RFC 8785, JCS) — focused subset sufficient
/// for the FR-028 audit chain. Produces a deterministic, whitespace-free
/// representation by sorting object keys lexicographically (by code-point
/// order), recursing through arrays and objects, normalizing numbers via
/// the JSON shortest-roundtrip form, and escaping strings minimally.
///
/// The audit-emitter (server-side) and the verifier (potentially running on
/// a clean inspector machine) MUST use this same implementation to recompute
/// the same hashes from the same logical payloads — see
/// contracts/audit-chain-verifier.md.
/// </summary>
public static class JsonCanonicalizer
{
    public static string Canonicalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        WriteElement(document.RootElement, sb);
        return sb.ToString();
    }

    private static void WriteElement(JsonElement element, StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, sb);
                break;
            case JsonValueKind.Array:
                WriteArray(element, sb);
                break;
            case JsonValueKind.String:
                WriteString(element.GetString()!, sb);
                break;
            case JsonValueKind.Number:
                WriteNumber(element, sb);
                break;
            case JsonValueKind.True:
                sb.Append("true");
                break;
            case JsonValueKind.False:
                sb.Append("false");
                break;
            case JsonValueKind.Null:
                sb.Append("null");
                break;
            case JsonValueKind.Undefined:
            default:
                throw new InvalidOperationException(
                    $"Unsupported JSON value kind in canonicalization: {element.ValueKind}");
        }
    }

    private static void WriteObject(JsonElement element, StringBuilder sb)
    {
        // RFC 8785 §3.2.3: object keys sorted in ascending order by their
        // UTF-16 code-point sequence (lexicographic / ordinal).
        var members = element.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        sb.Append('{');
        for (var i = 0; i < members.Count; i++)
        {
            if (i > 0) sb.Append(',');
            WriteString(members[i].Name, sb);
            sb.Append(':');
            WriteElement(members[i].Value, sb);
        }
        sb.Append('}');
    }

    private static void WriteArray(JsonElement element, StringBuilder sb)
    {
        sb.Append('[');
        var first = true;
        foreach (var item in element.EnumerateArray())
        {
            if (!first) sb.Append(',');
            WriteElement(item, sb);
            first = false;
        }
        sb.Append(']');
    }

    private static void WriteString(string value, StringBuilder sb)
    {
        // Minimal JSON escaping per RFC 8259 + RFC 8785 §3.2.2.2.
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append('"');
    }

    private static void WriteNumber(JsonElement element, StringBuilder sb)
    {
        // RFC 8785 §3.2.2.3: numbers in shortest ECMAScript form. For our
        // audit-chain payloads (integers + bounded decimals), the simplest
        // correct strategy is decimal-roundtrip. Integers stay integers;
        // fractions keep their minimal form.
        if (element.TryGetInt64(out var i64))
        {
            sb.Append(i64.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            var d = element.GetDecimal();
            // .NET's "G" format on decimal gives the shortest roundtrip.
            sb.Append(d.ToString("G", CultureInfo.InvariantCulture));
        }
    }
}
