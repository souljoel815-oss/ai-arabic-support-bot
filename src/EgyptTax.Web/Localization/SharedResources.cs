namespace EgyptTax.Web.Localization;

/// <summary>
/// Marker class for <c>IStringLocalizer&lt;SharedResources&gt;</c>.
/// The matching <c>SharedResources.{culture}.resx</c> files in the
/// same folder hold the bilingual strings the Blazor UI consumes via
/// dependency injection. The strings here are the **UI shell** —
/// nav labels, button labels, common error messages — that every
/// page needs; domain terminology lives in the
/// <c>terminology.{ar,en}.json</c> files in <c>EgyptTax.SharedKernel</c>
/// per R-19 because it's also consumed by the PDF renderer + ETA
/// generator (non-UI surfaces) so the JSON shape is more portable
/// than .resx.
/// </summary>
public sealed class SharedResources
{
    private SharedResources() { }
}
