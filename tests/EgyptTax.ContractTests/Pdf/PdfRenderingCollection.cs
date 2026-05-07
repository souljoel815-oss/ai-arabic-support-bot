namespace EgyptTax.ContractTests.Pdf;

/// <summary>
/// xUnit serializes tests within a Collection. Multi-class PDF
/// renders racing in parallel produced flaky empty-text extractions
/// on the first run after a fresh build (PdfPig + QuestPDF share
/// some global state during cold-start font + glyph caching). All
/// PDF-rendering test classes live in this single collection so they
/// run sequentially; per-class tests within each class are still
/// sequential by default.
/// </summary>
// CA1711 wants type names not ending in "Collection" — but xUnit's
// [CollectionDefinition] convention names the class after the
// collection it defines (e.g. SqlServerCollection elsewhere in this
// repo). Suppress for this well-known framework idiom.
#pragma warning disable CA1711
[CollectionDefinition(Name)]
public sealed class PdfRenderingCollection
{
    public const string Name = "PdfRendering";
}
#pragma warning restore CA1711
