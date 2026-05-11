using EgyptTax.Application.Banking.Parsers;

namespace EgyptTax.Application.Banking;

/// <summary>
/// G1.3 — finds the right <see cref="IBankStatementParser"/> for an
/// uploaded file by reading its first non-blank line and asking each
/// registered parser if it recognises the header.
///
/// Order matters when two parsers could match — they're tried in
/// registration order. Default order: CIB, NBE, QNB (frequency of
/// use in Egyptian SMB accounts). Add new parsers to the end of the
/// list when they ship.
/// </summary>
public static class BankStatementParserRegistry
{
    private static readonly IReadOnlyList<IBankStatementParser> _parsers =
        new IBankStatementParser[]
        {
            new CibBankStatementParser(),
            new NbeBankStatementParser(),
            new QnbBankStatementParser(),
        };

    public static IReadOnlyList<IBankStatementParser> All => _parsers;

    /// <summary>Sniff the first non-blank line; return the first
    /// parser that says <see cref="IBankStatementParser.CanParse"/>.
    /// Returns null if no parser recognises the file.</summary>
    public static IBankStatementParser? Detect(string fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent)) return null;
        using var reader = new StringReader(fileContent);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            return _parsers.FirstOrDefault(p => p.CanParse(line));
        }
        return null;
    }

    /// <summary>Lookup by exact bank name (used when the operator
    /// overrides auto-detect via the dropdown).</summary>
    public static IBankStatementParser? ByName(string bankName) =>
        _parsers.FirstOrDefault(p =>
            string.Equals(p.BankName, bankName, StringComparison.OrdinalIgnoreCase));
}
