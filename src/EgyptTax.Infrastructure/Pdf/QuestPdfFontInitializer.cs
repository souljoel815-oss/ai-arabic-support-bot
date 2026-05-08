using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf;

/// <summary>
/// Single shared QuestPDF static-init point: license activation +
/// Cairo / Amiri embedded-TTF registration. Every PDF renderer in
/// this assembly (the sales-invoice renderer, the five register
/// renderers, future register/filing renderers) calls
/// <see cref="EnsureRegistered"/> from its static constructor so
/// the work happens exactly once per process — repeated
/// FontManager.RegisterFont calls would needlessly rebuild Skia's
/// internal typeface map.
///
/// Diagnosed against parallel test execution where short-lived
/// resource streams produced empty rendered text: SkiaSharp holds
/// the stream by reference for lazy glyph lookup, so the byte
/// buffers MUST stay alive for the life of the process. The
/// <see cref="_fontBuffers"/> static list keeps them rooted.
/// </summary>
internal static class QuestPdfFontInitializer
{
    public const string PrimaryFontFamily = "Cairo";
    public const string ArabicFallbackFamily = "Amiri";

    private static readonly List<byte[]> _fontBuffers = new();

    private static readonly Lazy<bool> _initialized = new(() =>
    {
        QuestPDF.Settings.License = LicenseType.Community;
        RegisterEmbeddedFont("Pdf.Fonts.Cairo-Regular.ttf");
        RegisterEmbeddedFont("Pdf.Fonts.Amiri-Regular.ttf");
        RegisterEmbeddedFont("Pdf.Fonts.Amiri-Bold.ttf");
        return true;
    });

    public static void EnsureRegistered() => _ = _initialized.Value;

    private static void RegisterEmbeddedFont(string relativeName)
    {
        var assembly = typeof(QuestPdfFontInitializer).Assembly;
        var resourceName = $"{assembly.GetName().Name}.{relativeName}";
        using var resourceStream =
            assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded font resource '{resourceName}' was not found. Check the EmbeddedResource Link in EgyptTax.Infrastructure.csproj."
            );
        using var buffer = new MemoryStream();
        resourceStream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var liveStream = new MemoryStream(bytes, writable: false);
        FontManager.RegisterFont(liveStream);
        _fontBuffers.Add(bytes);
    }
}
