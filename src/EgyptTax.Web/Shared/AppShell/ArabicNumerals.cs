namespace EgyptTax.Web.Shared.AppShell;

/// <summary>
/// v5 UI Sprint 1 — converts Western digits to Eastern Arabic-Indic
/// digits (٠١٢٣٤٥٦٧٨٩) so amounts in the Dashboard match the Manus
/// 2026-05-15 mockups (e.g. ٢٤٥،٠٠٠ ج.م instead of 245,000 ج.م).
/// Used by the AppShell-scoped Arabic-locale views; legacy pages on
/// MainLayout keep their existing Western-digit formatting because
/// FR-005 requires Western digits on the legal-invoice PDF surface.
/// </summary>
public static class ArabicNumerals
{
    private const char ZeroAr = '٠';

    public static string ToArabic(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] >= '0' && chars[i] <= '9')
            {
                chars[i] = (char)(ZeroAr + (chars[i] - '0'));
            }
        }
        return new string(chars);
    }

    /// <summary>Format <paramref name="value"/> with the given format
    /// specifier under invariant culture (so the comma stays as ","
    /// separator), then substitute Western digits with Arabic-Indic.</summary>
    public static string Format(decimal value, string format = "N0") =>
        ToArabic(value.ToString(format, System.Globalization.CultureInfo.InvariantCulture));

    public static string Format(int value, string format = "N0") =>
        ToArabic(value.ToString(format, System.Globalization.CultureInfo.InvariantCulture));
}
