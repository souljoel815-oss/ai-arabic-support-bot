using System.Globalization;

namespace EgyptTax.Application.Banking.Parsers;

/// <summary>
/// G1.3 — National Bank of Egypt (NBE) CSV export parser.
///
/// NBE's NBE-Online portal exports CSV with this header shape:
///
///   Transaction Date,Description,Withdrawal,Deposit,Balance,Reference No
///
/// Fields are NOT quoted (NBE assumes descriptions don't contain
/// commas — which sometimes breaks, hence the line-count warning).
/// Dates are YYYY-MM-DD (ISO). Amounts use period decimal with no
/// thousand separators.
///
/// "Withdrawal" = our debit (money leaving the account); "Deposit"
/// = credit. Empty cells appear as nothing between commas (",,").
/// </summary>
public sealed class NbeBankStatementParser : IBankStatementParser
{
    public string BankName => "NBE";

    public bool CanParse(string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine)) return false;
        var normalised = firstLine.Trim();
        return normalised.Contains("Transaction Date", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Withdrawal", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Deposit", StringComparison.OrdinalIgnoreCase)
            && normalised.Contains("Balance", StringComparison.OrdinalIgnoreCase);
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

            // NBE uses plain comma split; descriptions with commas
            // produce a column-count mismatch we surface as a warning.
            var fields = raw.Split(',').Select(f => f.Trim()).ToList();
            if (fields.Count < 6)
            {
                warnings.Add($"Line {lineNo}: expected 6 columns, saw {fields.Count} — likely a comma inside the description. Skipped.");
                continue;
            }

            if (!DateOnly.TryParseExact(fields[0], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                warnings.Add($"Line {lineNo}: unparseable date '{fields[0]}'. Skipped.");
                continue;
            }
            var description = fields[1];
            var withdrawal = CibBankStatementParser.ParseDecimalOrZero(fields[2]);
            var deposit = CibBankStatementParser.ParseDecimalOrZero(fields[3]);
            var balance = CibBankStatementParser.ParseDecimalOrZero(fields[4]);
            var refNo = string.IsNullOrWhiteSpace(fields[5]) ? null : fields[5];

            if (withdrawal > 0m && deposit > 0m)
            {
                warnings.Add($"Line {lineNo}: both withdrawal and deposit > 0 — ambiguous, skipped.");
                continue;
            }

            lines.Add(new ParsedBankLine(
                TransactionDate: date,
                Description: description,
                Debit: withdrawal,
                Credit: deposit,
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
