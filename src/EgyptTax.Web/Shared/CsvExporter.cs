using System.Buffers;
using System.Globalization;
using System.Text;

namespace EgyptTax.Web.Shared;

/// <summary>
/// L2 (v3 roadmap) — CSV export helper. Anti-lock-in card for the
/// universal Egyptian SMB objection: "هل أقدر آخد بياناتي لو سبت
/// البرنامج؟" (can I take my data if I leave?). One shared utility
/// every page can call to dump a list as RFC 4180 CSV.
///
/// Always emits UTF-8 BOM so Excel-Arabic opens it without
/// double-clicking through "import wizard" charset selection. The
/// BOM is the difference between "double-click → readable Arabic"
/// and "double-click → ÙØ§ØªÙˆØ±Ø© garbage" for non-technical users.
/// </summary>
public static class CsvExporter
{
    private static readonly SearchValues<char> CsvSpecials = SearchValues.Create(",\"\n\r");

    /// <summary>
    /// Build a CSV byte[] from a list of rows. Each row is rendered
    /// by <paramref name="rowSelector"/> into an array of cells in
    /// the same order as <paramref name="headers"/>. Returns the
    /// bytes ready to push to JS for browser download.
    /// </summary>
    public static byte[] Build<T>(
        IEnumerable<string> headers,
        IEnumerable<T> rows,
        Func<T, IEnumerable<object?>> rowSelector)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(rowSelector);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            var cells = rowSelector(row).Select(FormatCell).Select(EscapeCsv);
            sb.AppendLine(string.Join(",", cells));
        }
        // UTF-8 with BOM — Excel-Arabic only respects the BOM, not
        // an explicit charset declaration.
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private static string FormatCell(object? value) => value switch
    {
        null => "",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal m => m.ToString("F2", CultureInfo.InvariantCulture),
        double d => d.ToString("F4", CultureInfo.InvariantCulture),
        float f => f.ToString("F4", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        Guid g => g.ToString("D"),
        _ => value.ToString() ?? "",
    };

    /// <summary>
    /// RFC 4180 CSV escaping: quote any cell containing comma, quote,
    /// newline; double-up any embedded quotes. Cheap + boring.
    /// </summary>
    private static string EscapeCsv(string cell)
    {
        if (string.IsNullOrEmpty(cell)) return "";
        if (cell.AsSpan().IndexOfAny(CsvSpecials) < 0) return cell;
        return "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string EscapeCsv(object? cell) => EscapeCsv(cell?.ToString() ?? "");
}
