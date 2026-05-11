using System.Globalization;

namespace EgyptTax.Application.Banking.Parsers;

/// <summary>
/// G1.3 — Qatar National Bank Al Ahli (QNB) CSV export parser.
///
/// QNB's portal exports use a pipe delimiter (unusual but consistent
/// across their statement / cheque / WPS exports — avoids the
/// description-with-commas problem NBE has):
///
///   Posting Date|Description|Debit Amount|Credit Amount|Closing Balance|Cheque/Reference
///
/// Dates are DD/MM/YYYY. Amounts: period decimal, no thousands.
/// Empty cells appear as a single space or nothing between pipes.
/// </summary>
public sealed class QnbBankStatementParser : IBankStatementParser
{
    public string BankName => "QNB";

    public bool CanParse(string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine)) return false;
        if (!firstLine.Contains('|')) return false;
        var normalised = firstLine.Trim();
        return normalised.Contains("Posting Date", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Debit Amount", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Credit Amount", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Closing Balance", StringComparison.OrdinalIgnoreCase);
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
                continue;
            }

            var fields = raw.Split('|').Select(f => f.Trim()).ToList();
            if (fields.Count < 6)
            {
                warnings.Add($"Line {lineNo}: expected 6 columns, saw {fields.Count}. Skipped.");
                continue;
            }

            if (!CibBankStatementParser.TryParseDate(fields[0], out var date))
            {
                warnings.Add($"Line {lineNo}: unparseable date '{fields[0]}'. Skipped.");
                continue;
            }
            var description = fields[1];
            var debit = CibBankStatementParser.ParseDecimalOrZero(fields[2]);
            var credit = CibBankStatementParser.ParseDecimalOrZero(fields[3]);
            var balance = CibBankStatementParser.ParseDecimalOrZero(fields[4]);
            var refNo = string.IsNullOrWhiteSpace(fields[5]) ? null : fields[5];

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
                RunningBalance: balance,
                BankReference: refNo));
        }

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
}
