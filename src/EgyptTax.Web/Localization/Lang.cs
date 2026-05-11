using System.Globalization;

namespace EgyptTax.Web.Localization;

/// <summary>
/// Single-language UX helper. Pages render either Arabic OR English
/// based on CultureInfo.CurrentCulture (set via the /set-culture
/// cookie writer in Program.cs). This sidesteps the inline "EN / AR"
/// bilingual pattern that earlier pages used and gives operators a
/// clean monolingual experience.
/// </summary>
public static class Lang
{
    public static bool IsArabic =>
        CultureInfo.CurrentCulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

    public static string Tr(string ar, string en) => IsArabic ? ar : en;
}
