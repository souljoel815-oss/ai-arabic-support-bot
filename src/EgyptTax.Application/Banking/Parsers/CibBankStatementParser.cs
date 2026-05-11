using System.Globalization;

namespace EgyptTax.Application.Banking.Parsers;

/// <summary>
/// G1.3 — Commercial International Bank (CIB) CSV export parser.
///
/// CIB's online-banking "Account Statement → Export CSV" produces a
/// header line in this shape (column order is stable, capitalisation
/// varies by export year):
///
///   "Date","Value Date","Description","Reference","Debit","Credit","Running Balance"
///
/// Fields are double-quoted, comma-separated. Dates are DD/MM/YYYY.
/// Amounts use period decimal separator and may include thousands
/// commas inside the quotes ("1,234.56"). Empty cells appear as ""
/// in the unused column (debit OR credit per row, never both).
///
/// This parser is conservative — when the format deviates from the
/// canonical template, it logs a Warning rather than throwing, so
/// the operator sees what couldn't be parsed and can hand-correct
/// in the preview grid.
/// </summary>
public sealed class CibBankStatementParser : IBankStatementParser
{
    public string BankName => "CIB";

    public bool CanParse(string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine)) return false;
        var normalised = firstLine.Replace("\"", "").Trim();
        // Header must contain every CIB column name (English locale).
        return normalised.Contains("Date", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Description", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Debit", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Credit", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Running Balance", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Value Date", StringComparison.OrdinalIgnoreCase);
    }

    public BankStatementParseResult Parse(string fileContent)
    {
        var lines = new List<ParsedBankLine>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return new BankStatementParseResult(null, null, null, null, lines, warnings);
        }

        using var reader = new StringReader(fileContent);
        string? raw;
        var lineNo = 0;
        var seenHeader = false;

        while ((raw = reader.ReadLine()) is not null)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!seenHeader)
            {
                seenHeader = true;
                continue; // skip the header row
            }

            var fields = ParseCsvLine(raw);
            if (fields.Count < 7)
            {
                warnings.Add($"Line {lineNo}: expected 7 columns, saw {fields.Count}. Skipped.");
                continue;
            }

            if (!TryParseDate(fields[0], out var date))
            {
                warnings.Add($"Line {lineNo}: unparseable date '{fields[0]}'. Skipped.");
                continue;
            }
            var description = fields[2];
            var bankRef = string.IsNullOrWhiteSpace(fields[3]) ? null : fields[3];
            var debit = ParseDecimalOrZero(fields[4]);
            var credit = ParseDecimalOrZero(fields[5]);
            var running = ParseDecimalOrZero(fields[6]);

            if (debit > 0m && credit > 0m)
            {
                warnings.Add($"Line {lineNo}: both debit and credit > 0 — ambiguous, skipped.");
                continue;
            }

            lines.Add(new ParsedBankLine(
                TransactionDate: date,
                Description: description,
                Debit: debit,
                Credit: credit,
                RunningBalance: running,
                BankReference: bankRef));
        }

        // CIB header CSV doesn't carry an explicit period or opening
        // balance row — derive from the first/last transaction date,
        // leave opening/closing blank for the operator to confirm.
        DateOnly? periodStart = lines.Count > 0 ? lines.Min(l => l.TransactionDate) : null;
        DateOnly? periodEnd = lines.Count > 0 ? lines.Max(l => l.TransactionDate) : null;

        return new BankStatementParseResult(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            OpeningBalance: null,
            ClosingBalance: lines.Count > 0 ? lines[^1].RunningBalance : null,
            Lines: lines,
            Warnings: warnings);
    }

    /// <summary>Minimal CSV split honoring double-quoted fields with
    /// embedded commas. Skips RFC 4180 edge cases (escaped quotes
    /// inside fields) because CIB exports don't use them in
    /// practice — fall back to comma split when no quotes present.</summary>
    internal static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == ',' && !inQuotes)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
                continue;
            }
            sb.Append(ch);
        }
        fields.Add(sb.ToString().Trim());
        return fields;
    }

    internal static bool TryParseDate(string raw, out DateOnly date)
    {
        // CIB uses DD/MM/YYYY consistently.
        if (DateOnly.TryParseExact(raw.Trim(), "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }
        // Fallback: ISO-shaped dates for newer exports.
        return DateOnly.TryParse(raw.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    internal static decimal ParseDecimalOrZero(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        var cleaned = raw.Replace(",", "", StringComparison.Ordinal).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number,
            CultureInfo.InvariantCulture, out var n) ? n : 0m;
    }
}
