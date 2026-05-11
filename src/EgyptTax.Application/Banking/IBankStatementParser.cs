namespace EgyptTax.Application.Banking;

/// <summary>
/// G1.3 — pluggable parser for a single Egyptian bank's exported
/// statement file (CSV today; OFX/XML possible later behind the same
/// interface). Each implementation knows the column layout, date
/// format, and decimal convention of one bank.
///
/// Parsers are <b>pure</b> — string in, structured rows out — so
/// they're trivially unit-testable against fixture strings and don't
/// touch the DB. The page layer takes the result and feeds it through
/// the same <c>BankStatement</c> aggregate the manual-entry form
/// already uses.
/// </summary>
public interface IBankStatementParser
{
    /// <summary>Bank display name shown in the dropdown / preview.
    /// Stable per parser instance (no localisation here — UI handles
    /// Arabic/English).</summary>
    string BankName { get; }

    /// <summary>Pick parser by sniffing the first non-empty line of
    /// the upload (header row). Returns <c>true</c> if this parser
    /// recognises the layout; the registry picks the first true.</summary>
    bool CanParse(string firstLine);

    BankStatementParseResult Parse(string fileContent);
}

/// <summary>Result of a single parse attempt. The header values
/// (period, opening, closing) are best-effort — some bank exports
/// omit them, in which case the page form prompts the operator to
/// fill manually.</summary>
public sealed record BankStatementParseResult(
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    IReadOnlyList<ParsedBankLine> Lines,
    IReadOnlyList<string> Warnings);

public sealed record ParsedBankLine(
    DateOnly TransactionDate,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? BankReference);
