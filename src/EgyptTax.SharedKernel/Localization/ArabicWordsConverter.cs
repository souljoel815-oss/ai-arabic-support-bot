using System.Globalization;

namespace EgyptTax.SharedKernel.Localization;

/// <summary>
/// FR-014 / R-12 — converts a non-negative integer or decimal monetary
/// amount to its Arabic-language words representation, with the
/// EGP-specific suffixes ("جنيها مصريا" / "قرشا" / "فقط لا غير")
/// expected on legal Egyptian invoices. Uses the modern simplified
/// agreement rules common in Egyptian invoicing software:
///   * 1 → singular form ("جنيه مصري واحد")
///   * 2 → dual form ("جنيهان مصريان")
///   * 3-10 → plural with sound feminine "جنيهات مصرية"
///   * 11+ → singular accusative tamyiz "جنيها مصريا"
/// Strict classical gender-of-counted-noun rules (3-10 take opposite
/// gender of the singular) are intentionally simplified — the modern
/// form above is what end users see on bills and SaaS invoices today.
/// </summary>
public static class ArabicWordsConverter
{
    private const string Conjunction = " و";

    private static readonly string[] Ones =
    {
        string.Empty, "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة",
        "ستة", "سبعة", "ثمانية", "تسعة",
    };

    private static readonly string[] Teens =
    {
        "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر",
        "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر",
    };

    private static readonly string[] Tens =
    {
        string.Empty, string.Empty, "عشرون", "ثلاثون", "أربعون", "خمسون",
        "ستون", "سبعون", "ثمانون", "تسعون",
    };

    private static readonly string[] Hundreds =
    {
        string.Empty, "مائة", "مئتان", "ثلاثمائة", "أربعمائة", "خمسمائة",
        "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة",
    };

    public static string FromInteger(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value),
                "FR-014 amount-in-words is only defined for non-negative integers.");
        }
        if (value == 0)
        {
            return "صفر";
        }

        var billions = (int)(value / 1_000_000_000L);
        var millions = (int)((value / 1_000_000L) % 1000L);
        var thousands = (int)((value / 1000L) % 1000L);
        var ones = (int)(value % 1000L);

        var parts = new List<string>(4);
        if (billions > 0)
        {
            parts.Add(BuildScaleGroup(billions, singular: "مليار", dual: "ملياران",
                pluralFor3To10: "مليارات", singularAccusative: "مليارا"));
        }
        if (millions > 0)
        {
            parts.Add(BuildScaleGroup(millions, singular: "مليون", dual: "مليونان",
                pluralFor3To10: "ملايين", singularAccusative: "مليونا"));
        }
        if (thousands > 0)
        {
            parts.Add(BuildScaleGroup(thousands, singular: "ألف", dual: "ألفان",
                pluralFor3To10: "آلاف", singularAccusative: "ألفا"));
        }
        if (ones > 0)
        {
            parts.Add(ConvertGroup3(ones));
        }

        return string.Join(Conjunction, parts);
    }

    public static string FromEgyptianPounds(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount),
                "FR-014 amount-in-words is only defined for non-negative monetary amounts.");
        }

        // Round to qirsh (1/100 EGP) using banker's rounding so the textual
        // form lines up with MoneyEgp's stored value.
        var rounded = Math.Round(amount, 2, MidpointRounding.ToEven);
        var pounds = (long)Math.Floor(rounded);
        var piasters = (int)Math.Round((rounded - pounds) * 100m, 0, MidpointRounding.ToEven);

        if (pounds == 0 && piasters == 0)
        {
            return "صفر جنيه مصري فقط لا غير";
        }

        var parts = new List<string>(2);
        if (pounds > 0)
        {
            parts.Add(BuildCurrencyPhrase(pounds,
                singular: "جنيه مصري واحد",
                dual: "جنيهان مصريان",
                pluralFor3To10Suffix: "جنيهات مصرية",
                singularAccusativeSuffix: "جنيها مصريا"));
        }
        if (piasters > 0)
        {
            parts.Add(BuildCurrencyPhrase(piasters,
                singular: "قرش واحد",
                dual: "قرشان",
                pluralFor3To10Suffix: "قروش",
                singularAccusativeSuffix: "قرشا"));
        }

        return string.Join(Conjunction, parts) + " فقط لا غير";
    }

    private static string ConvertGroup3(int n)
    {
        if (n <= 0)
        {
            return string.Empty;
        }

        var hundreds = n / 100;
        var rest = n % 100;
        var parts = new List<string>(2);

        if (hundreds > 0)
        {
            parts.Add(Hundreds[hundreds]);
        }

        if (rest > 0)
        {
            parts.Add(ConvertBelow100(rest));
        }

        return string.Join(Conjunction, parts);
    }

    private static string ConvertBelow100(int n)
    {
        if (n < 10)
        {
            return Ones[n];
        }
        if (n < 20)
        {
            return Teens[n - 10];
        }

        var t = n / 10;
        var o = n % 10;
        return o == 0
            ? Tens[t]
            : Ones[o] + Conjunction + Tens[t];
    }

    private static string BuildScaleGroup(
        int count,
        string singular,
        string dual,
        string pluralFor3To10,
        string singularAccusative)
    {
        if (count == 1)
        {
            return singular;
        }
        if (count == 2)
        {
            return dual;
        }
        if (count >= 3 && count <= 9)
        {
            return Ones[count] + " " + pluralFor3To10;
        }
        if (count == 10)
        {
            // "عشرة" is in the Teens array slot 0; Ones stops at 9.
            return Teens[0] + " " + pluralFor3To10;
        }

        // 11+ — agreement is driven by the LAST two digits (the "tamyiz"
        // tail). 11..99 → singular accusative ("أحد عشر ألفا" / "مائة
        // وخمسة وعشرون ألفا"); exact multiples of 100 (200, 300, …) →
        // plain singular ("مائة ألف"); 103-style numbers whose tail
        // lands in 3..10 → plural ("مائة وثلاثة آلاف").
        var lastTwo = count % 100;
        if (lastTwo == 0)
        {
            return ConvertGroup3(count) + " " + singular;
        }
        if (lastTwo >= 3 && lastTwo <= 10)
        {
            return ConvertGroup3(count) + " " + pluralFor3To10;
        }
        return ConvertGroup3(count) + " " + singularAccusative;
    }

    private static string BuildCurrencyPhrase(
        long count,
        string singular,
        string dual,
        string pluralFor3To10Suffix,
        string singularAccusativeSuffix)
    {
        return count switch
        {
            1 => singular,
            2 => dual,
            >= 3 and <= 10 => FromInteger(count) + " " + pluralFor3To10Suffix,
            _ => FromInteger(count) + " " + singularAccusativeSuffix,
        };
    }

    /// <summary>
    /// Convenience for callers using <see cref="MoneyEgp"/> — round-trips
    /// through the same <see cref="FromEgyptianPounds(decimal)"/> path.
    /// </summary>
    public static string FromEgyptianPounds(MoneyEgp amount, CultureInfo? _ = null) =>
        FromEgyptianPounds(amount.Amount);
}
