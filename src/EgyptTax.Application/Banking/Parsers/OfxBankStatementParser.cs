using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace EgyptTax.Application.Banking.Parsers;

/// <summary>
/// v4 C.8 — bank-agnostic OFX 2.x (XML) statement parser. Most
/// modern Egyptian bank export portals (CIB / NBE / QNB online
/// banking) emit OFX 2.x for the "Download statement" action;
/// matching against any specific bank schema isn't necessary
/// because OFX is itself a standard.
///
/// We deliberately scope to OFX 2.x XML — OFX 1.x is SGML
/// (open tags without close tags, e.g. <c>&lt;DTPOSTED&gt;...</c>
/// alone on a line) and would need a separate dialect-aware
/// parser. When a 1.x file lands here we surface a clear warning
/// rather than try to half-parse it.
///
/// Sign convention: OFX <c>TRNAMT</c> is signed — negative =
/// money out (we map to debit), positive = money in (credit).
/// The optional <c>TRNTYPE</c> is treated as advisory because
/// some banks emit both DEBIT/CREDIT and inconsistent signs.
/// </summary>
public sealed class OfxBankStatementParser : IBankStatementParser
{
    public string BankName => "OFX (any bank)";

    public bool CanParse(string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine)) return false;
        var trimmed = firstLine.TrimStart('﻿', ' ', '\t');
        // OFX 2.x always starts with the XML PI; the OFX PI tag is
        // optional. Either signal is enough — if the file later
        // turns out to not be OFX, Parse will Warning + return.
        return trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<?OFX", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<OFX", StringComparison.OrdinalIgnoreCase);
    }

    public BankStatementParseResult Parse(string fileContent)
    {
        var lines = new List<ParsedBankLine>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return new BankStatementParseResult(null, null, null, null, lines, warnings);
        }

        // Trim BOM + leading whitespace before XmlDocument balks.
        var content = fileContent.TrimStart('﻿', ' ', '\t', '\r', '\n');

        // Heuristic for OFX 1.x SGML: starts with "OFXHEADER:" key/
        // value pairs (not XML). We don't support the SGML dialect
        // — surface a clear warning so the operator knows they need
        // a 2.x export from the bank portal.
        if (content.StartsWith("OFXHEADER:", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("File is OFX 1.x (SGML). DaftarX supports OFX 2.x (XML) only — "
                + "re-export from the bank portal selecting the OFX 2 / XML option.");
            return new BankStatementParseResult(null, null, null, null, lines, warnings);
        }

        // Strip the optional <?OFX ... ?> processing instruction —
        // XmlReader treats unknown PIs as fine, but some bank exports
        // emit a malformed PI that trips the parser; safest to drop.
        var ofxPiIndex = content.IndexOf("<?OFX", StringComparison.OrdinalIgnoreCase);
        if (ofxPiIndex >= 0)
        {
            var endPi = content.IndexOf("?>", ofxPiIndex, StringComparison.Ordinal);
            if (endPi > ofxPiIndex)
            {
                content = content.Remove(ofxPiIndex, endPi - ofxPiIndex + 2);
            }
        }

        XDocument doc;
        try
        {
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = true,
            });
            doc = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            warnings.Add($"OFX file is not valid XML: {ex.Message}. "
                + "Re-export from the bank portal selecting OFX 2 / XML.");
            return new BankStatementParseResult(null, null, null, null, lines, warnings);
        }

        // OFX is XML but the element names are uppercase + unscoped;
        // walking by LocalName makes the parser tolerant of optional
        // OFX namespaces some exports add.
        var transactions = doc.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "STMTTRN", StringComparison.Ordinal))
            .ToList();

        foreach (var tx in transactions)
        {
            var dateRaw = ChildValue(tx, "DTPOSTED")
                ?? ChildValue(tx, "DTUSER")
                ?? ChildValue(tx, "DTAVAIL");
            if (!TryParseOfxDate(dateRaw, out var date))
            {
                warnings.Add($"Transaction skipped: unparseable DTPOSTED '{dateRaw}'.");
                continue;
            }

            var amtRaw = ChildValue(tx, "TRNAMT");
            if (!decimal.TryParse(amtRaw, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var amount))
            {
                warnings.Add($"Transaction on {date:yyyy-MM-dd} skipped: unparseable TRNAMT '{amtRaw}'.");
                continue;
            }

            var name = ChildValue(tx, "NAME");
            var memo = ChildValue(tx, "MEMO");
            var description = string.IsNullOrWhiteSpace(name) ? memo ?? "" :
                (string.IsNullOrWhiteSpace(memo) ? name : $"{name} — {memo}");

            var fitId = ChildValue(tx, "FITID");

            // Negative TRNAMT = money out (debit on our side); positive
            // = money in (credit). Magnitudes go into the matching
            // column; the existing pipeline treats both as non-negative.
            var debit = amount < 0m ? Math.Abs(amount) : 0m;
            var credit = amount > 0m ? amount : 0m;

            lines.Add(new ParsedBankLine(
                TransactionDate: date,
                Description: description,
                Debit: debit,
                Credit: credit,
                RunningBalance: 0m,
                BankReference: fitId));
        }

        // Period + closing balance often appear at the STMTRS level.
        var stmtrs = doc.Descendants()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, "STMTRS", StringComparison.Ordinal));

        DateOnly? periodStart = null;
        DateOnly? periodEnd = null;
        decimal? closingBalance = null;

        if (stmtrs is not null)
        {
            var tranList = stmtrs.Descendants()
                .FirstOrDefault(x => string.Equals(x.Name.LocalName, "BANKTRANLIST", StringComparison.Ordinal));
            if (tranList is not null)
            {
                if (TryParseOfxDate(ChildValue(tranList, "DTSTART"), out var ps)) periodStart = ps;
                if (TryParseOfxDate(ChildValue(tranList, "DTEND"), out var pe)) periodEnd = pe;
            }

            var ledger = stmtrs.Descendants()
                .FirstOrDefault(x => string.Equals(x.Name.LocalName, "LEDGERBAL", StringComparison.Ordinal));
            var balRaw = ledger is not null ? ChildValue(ledger, "BALAMT") : null;
            if (decimal.TryParse(balRaw, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var bal))
            {
                closingBalance = bal;
            }
        }

        // If the file didn't carry a period, infer from transactions.
        if (periodStart is null && lines.Count > 0)
        {
            periodStart = lines.Min(l => l.TransactionDate);
        }
        if (periodEnd is null && lines.Count > 0)
        {
            periodEnd = lines.Max(l => l.TransactionDate);
        }

        return new BankStatementParseResult(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            OpeningBalance: null,
            ClosingBalance: closingBalance,
            Lines: lines,
            Warnings: warnings);
    }

    private static string? ChildValue(XElement parent, string localName)
    {
        var child = parent.Elements()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.Ordinal));
        return child?.Value.Trim();
    }

    /// <summary>OFX dates are <c>YYYYMMDD</c>, <c>YYYYMMDDHHMMSS</c>,
    /// or <c>YYYYMMDDHHMMSS.XXX[GMT]</c>. We only care about the
    /// date portion; cut at any non-digit after the leading 8.</summary>
    internal static bool TryParseOfxDate(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var span = raw.AsSpan().Trim();
        if (span.Length < 8) return false;
        var datePortion = span.Slice(0, 8);
        return DateOnly.TryParseExact(datePortion, "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
