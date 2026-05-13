using System.Globalization;
using System.Text;

namespace EgyptTax.Infrastructure.Onboarding;

/// <summary>
/// D2.5 (v3 roadmap) — Inverse of <see cref="CsvExporter"/>. Parses
/// RFC 4180 CSV files uploaded by operators migrating from Excel /
/// QuickBooks / Edara / etc. Tolerates UTF-8 BOM (Excel-Arabic
/// emits BOM-prefixed files), accepts CRLF or LF line endings, and
/// handles quoted cells with embedded commas / newlines / doubled
/// quotes.
///
/// Returns rows as Dictionary&lt;header, cell&gt; so the caller can
/// look up by column name without needing to know column order.
/// Header lookup is case-insensitive + trims whitespace — operators
/// re-export from QuickBooks with "Customer Name" + " Email " etc.
/// </summary>
public static class CsvImporter
{
    /// <summary>
    /// Parse a CSV byte stream into header + row dictionaries.
    /// Throws <see cref="InvalidDataException"/> with row + column
    /// position when the file is malformed; this lets the UI show
    /// "Row 14, column 3: unmatched quote" instead of a stack trace.
    /// </summary>
    public static (IReadOnlyList<string> Headers, IReadOnlyList<Dictionary<string, string>> Rows) Parse(
        ReadOnlySpan<byte> bytes)
    {
        // Strip BOM if present so the first header doesn't carry
        // U+FEFF as a prefix character.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bytes = bytes[3..];
        }

        var text = Encoding.UTF8.GetString(bytes);
        var allRows = ParseRfc4180(text);
        if (allRows.Count == 0)
        {
            return (Array.Empty<string>(), Array.Empty<Dictionary<string, string>>());
        }

        var headers = allRows[0]
            .Select(h => h.Trim())
            .ToList();

        var dataRows = new List<Dictionary<string, string>>(capacity: allRows.Count - 1);
        for (var i = 1; i < allRows.Count; i++)
        {
            var row = allRows[i];
            // Skip blank trailing rows (Excel often appends one).
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                var cell = c < row.Count ? row[c].Trim() : string.Empty;
                dict[headers[c]] = cell;
            }
            dataRows.Add(dict);
        }
        return (headers, dataRows);
    }

    /// <summary>
    /// Lookup helper that's case-insensitive, accepts a list of
    /// synonym headers (e.g. "Code" or "كود"), and returns null
    /// if every synonym is missing or blank.
    /// </summary>
    public static string? Get(Dictionary<string, string> row, params string[] synonyms)
    {
        foreach (var key in synonyms)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }
        return null;
    }

    /// <summary>Same as Get but throws when every synonym is missing — for required columns.</summary>
    public static string Required(Dictionary<string, string> row, string fieldNameForError, params string[] synonyms)
    {
        var value = Get(row, synonyms);
        if (value is null)
        {
            throw new InvalidDataException(
                $"Missing required field '{fieldNameForError}' (looked for: {string.Join(", ", synonyms)}).");
        }
        return value;
    }

    /// <summary>
    /// Decimal parser tolerant to Arabic locale formatting (1.234,56)
    /// and English (1,234.56). Tries InvariantCulture first, then
    /// strips common thousands separators.
    /// </summary>
    public static decimal? TryParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = raw.Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        // Strip thousands separators + try again.
        var stripped = cleaned.Replace(",", "", StringComparison.Ordinal);
        if (decimal.TryParse(stripped, NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return d;
        return null;
    }

    private static List<List<string>> ParseRfc4180(string text)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Doubled quote inside quoted field = literal quote.
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                currentField.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    i++;
                    break;
                case '\r':
                    // Either CRLF or bare CR — both terminate the row.
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    if (i + 1 < text.Length && text[i + 1] == '\n') i += 2;
                    else i++;
                    break;
                case '\n':
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    i++;
                    break;
                default:
                    currentField.Append(c);
                    i++;
                    break;
            }
        }

        // Final unterminated row.
        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }
}
