using System.Globalization;
using System.Text.RegularExpressions;

namespace EgyptTax.Application.Ocr;

/// <summary>
/// G3.2 — pulls the three fields the expense form needs from a
/// blob of OCR'd receipt text:
///   * <see cref="ReceiptDraft.TotalEgp"/> — largest plausible EGP
///     amount on the page. Receipts conventionally print the total
///     in bold/large type and it tends to be the biggest number, so
///     "largest amount with two decimal places" is a 70-80%
///     heuristic on Egyptian thermal receipts.
///   * <see cref="ReceiptDraft.Date"/> — first date-shaped string
///     found, supporting DD/MM/YYYY, DD-MM-YYYY, YYYY-MM-DD, and the
///     Arabic-digit variants.
///   * <see cref="ReceiptDraft.SupplierName"/> — the non-blank line
///     immediately above where the total appeared, since the total
///     is almost always at the bottom of a receipt and the line
///     above is the shop name (or address — fallback to first
///     non-blank line if the above is too generic like "TOTAL").
///
/// Pure: takes a string of OCR text + returns a value object.
/// Easy to unit-test without bringing in Tesseract.
/// </summary>
public static class ReceiptOcrExtractor
{
    // EGP amounts on Egyptian receipts: "123.45", "1,234.56", "EGP 12.50",
    // "12.50 ج.م.", and Arabic-digit variants like "١٢٣٫٤٥". Anchor on
    // two decimal places — the total ALWAYS shows piasters.
    private static readonly Regex AmountPattern = new(
        @"(?<![.\d])(?<int>[\d٠-٩]{1,7}(?:[,،][\d٠-٩]{3})*)[.,٫](?<frac>[\d٠-٩]{2})(?![.\d])",
        RegexOptions.Compiled);

    // DD/MM/YYYY, DD-MM-YYYY, YYYY-MM-DD (and arabic-digit variants).
    private static readonly Regex DatePattern = new(
        @"(?<a>[\d٠-٩]{1,4})[/\-\.](?<b>[\d٠-٩]{1,2})[/\-\.](?<c>[\d٠-٩]{2,4})",
        RegexOptions.Compiled);

    // Lines that match these aren't useful as a supplier name — they
    // are headers/footers that show up above the total. Case-insensitive.
    private static readonly Regex BoilerplateLine = new(
        @"^(total|sub[\s-]?total|net|grand|amount|paid|cash|change|vat|tax|invoice|receipt|الإجمالي|إجمالي|المبلغ|الصافي|الضريبة|الفاتورة|الإيصال|نقدي|الباقي|ضريبة|ج\.?م\.?|egp|le)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ReceiptDraft Extract(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return new ReceiptDraft(null, null, null, "(empty OCR result)");

        var normalised = NormaliseArabicDigits(ocrText);
        var lines = normalised.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        // --- Amount ---
        decimal? total = null;
        int totalLineIdx = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (Match m in AmountPattern.Matches(lines[i]))
            {
                var intPart = m.Groups["int"].Value.Replace(",", "").Replace("،", "");
                var fracPart = m.Groups["frac"].Value;
                if (!decimal.TryParse($"{intPart}.{fracPart}", NumberStyles.Number, CultureInfo.InvariantCulture, out var amt))
                    continue;
                // Reject anything that's clearly not a price: < 0.10 or > 1,000,000.
                if (amt < 0.10m || amt > 1_000_000m) continue;
                if (total is null || amt > total)
                {
                    total = amt;
                    totalLineIdx = i;
                }
            }
        }

        // --- Date ---
        DateOnly? date = null;
        foreach (var line in lines)
        {
            foreach (Match m in DatePattern.Matches(line))
            {
                if (TryParseReceiptDate(m.Groups["a"].Value, m.Groups["b"].Value, m.Groups["c"].Value, out var d))
                {
                    date = d;
                    break;
                }
            }
            if (date is not null) break;
        }

        // --- Supplier name ---
        // Egyptian receipts overwhelmingly print the shop name at the
        // TOP, not above the total. Try first-meaningful-line first;
        // walk-back-from-total is the fallback for layouts where the
        // header is just a logo image (no OCR'd text above).
        bool IsMeaningfulSupplierLine(string line) =>
            line.Length >= 3 &&
            !BoilerplateLine.IsMatch(line) &&
            !AmountPattern.IsMatch(line) &&
            !DatePattern.IsMatch(line) &&
            !LooksLikeColumnHeader(line);

        string? supplier = lines.FirstOrDefault(IsMeaningfulSupplierLine);

        if (supplier is null && totalLineIdx > 0)
        {
            for (int i = totalLineIdx - 1; i >= 0; i--)
            {
                if (IsMeaningfulSupplierLine(lines[i]))
                {
                    supplier = lines[i];
                    break;
                }
            }
        }

        var notes = (total, date, supplier) switch
        {
            (null, _, _) => "couldn't read a total amount",
            (_, null, _) => "couldn't read a date (today auto-filled by the form)",
            _ => null,
        };

        return new ReceiptDraft(total, date, supplier, notes);
    }

    /// <summary>Replace ٠-٩ with 0-9 so the downstream regexes only
    /// need to handle Latin digits. Arabic comma (،) → comma, Arabic
    /// decimal (٫) → period.</summary>
    private static string NormaliseArabicDigits(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c >= '٠' && c <= '٩') sb.Append((char)('0' + (c - '٠')));
            else if (c == '٫') sb.Append('.');
            else if (c == '،') sb.Append(',');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Detects column-header lines like "Item   Price" or "Qty Desc Total"
    /// — multiple short whitespace-separated tokens with no real content.
    /// Receipts use these between the header and the line items; they
    /// shouldn't be picked up as supplier names.
    /// </summary>
    private static bool LooksLikeColumnHeader(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return false;
        var headerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "item", "items", "qty", "quantity", "price", "amount", "desc",
            "description", "no", "code", "unit", "subtotal", "total",
            "الصنف", "الكمية", "السعر", "المبلغ", "الوصف", "البيان",
        };
        return tokens.All(t => headerWords.Contains(t));
    }

    private static bool TryParseReceiptDate(string a, string b, string c, out DateOnly date)
    {
        date = default;
        if (!int.TryParse(a, out var ai) || !int.TryParse(b, out var bi) || !int.TryParse(c, out var ci))
            return false;

        // YYYY-MM-DD if first group is 4 digits.
        if (a.Length == 4)
        {
            return TryBuildDate(ai, bi, ci, out date);
        }
        // DD-MM-YYYY when third is 4 digits, otherwise DD-MM-YY.
        var year = ci;
        if (c.Length == 2)
        {
            // Two-digit year: assume 2000s up to the current year + 1; otherwise 1900s.
            var pivot = (DateTime.UtcNow.Year + 1) % 100;
            year = ci <= pivot ? 2000 + ci : 1900 + ci;
        }
        return TryBuildDate(year, bi, ai, out date);
    }

    private static bool TryBuildDate(int year, int month, int day, out DateOnly date)
    {
        date = default;
        if (year is < 2000 or > 2099) return false;
        if (month is < 1 or > 12) return false;
        if (day is < 1 or > 31) return false;
        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}

/// <summary>What the regex extractor pulled out of an OCR'd image.
/// Any field may be null when extraction couldn't find a candidate;
/// the UI treats those as "operator types it manually".</summary>
public sealed record ReceiptDraft(
    decimal? TotalEgp,
    DateOnly? Date,
    string? SupplierName,
    string? ExtractorNote);
