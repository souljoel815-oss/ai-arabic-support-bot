using System.Globalization;

namespace EgyptTax.Web.Shared.AppShell;

/// <summary>
/// Converts Western digits to Arabic-Indic digits for display in
/// Arabic-locale views. Dashboard amounts render like ٢٤٥،٠٠٠
/// while legacy PDF/legal surfaces keep western digits.
/// </summary>
public static class ArabicNumerals
{
    private static readonly string[] ArabicDigits =
        { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };

    public static string ToArabic(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c >= '0' && c <= '9')
                result[i] = ArabicDigits[c - '0'][0];
            else
                result[i] = c;
        }
        return new string(result);
    }

    public static string Format(decimal value, string format = "N0")
    {
        var formatted = value.ToString(format, CultureInfo.InvariantCulture);
        return ToArabic(formatted);
    }

    public static string Format(int value, string format = "N0")
    {
        var formatted = value.ToString(format, CultureInfo.InvariantCulture);
        return ToArabic(formatted);
    }
}
