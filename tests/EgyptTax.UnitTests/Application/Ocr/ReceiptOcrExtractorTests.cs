using EgyptTax.Application.Ocr;

namespace EgyptTax.UnitTests.Application.Ocr;

/// <summary>
/// G3.2 — covers the regex extractor in isolation (no Tesseract).
/// Test corpus reflects what we've seen on real Egyptian thermal
/// receipts: mixed Arabic + Latin numerals, total at the bottom,
/// supplier name on a separate line, Western or Arabic dates.
/// </summary>
public class ReceiptOcrExtractorTests
{
    [Fact]
    public void Extract_NullOrBlank_ReturnsEmptyDraft()
    {
        var draft = ReceiptOcrExtractor.Extract("");
        draft.TotalEgp.Should().BeNull();
        draft.Date.Should().BeNull();
        draft.SupplierName.Should().BeNull();
        draft.ExtractorNote.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Extract_FindsTotalFromCleanReceipt()
    {
        var ocr = """
            Carrefour Egypt
            Cairo Branch — Maadi
            2026-04-15

            Item                Price
            Bread               12.50
            Milk                25.00
            Sub-total           37.50
            VAT 14%              5.25
            TOTAL               42.75
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);

        draft.TotalEgp.Should().Be(42.75m);
        draft.Date.Should().Be(new DateOnly(2026, 4, 15));
        draft.SupplierName.Should().Be("Carrefour Egypt");
    }

    [Fact]
    public void Extract_PicksLargestPlausibleAmountAsTotal()
    {
        // Per-item lines also have prices but the total at the bottom
        // is always the largest. The heuristic must pick the largest
        // not the last.
        var ocr = """
            Spinneys
            01/05/2026
            Eggs        15.00
            Cheese     250.00
            Total      265.00
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);
        draft.TotalEgp.Should().Be(265.00m);
    }

    [Fact]
    public void Extract_HandlesArabicDigitsAndComma()
    {
        // Arabic digits ٠-٩, Arabic decimal separator ٫, Arabic comma ،
        // for thousands. Real receipt formatting we've seen.
        var ocr = """
            بقالة الحاج محمد
            ١٥/٠٤/٢٠٢٦
            خبز         ١٢٫٥٠
            جبنة       ٢٥٠٫٠٠
            الإجمالي  ٢٦٢٫٥٠
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);

        draft.TotalEgp.Should().Be(262.50m);
        draft.Date.Should().Be(new DateOnly(2026, 4, 15));
        draft.SupplierName.Should().Be("بقالة الحاج محمد");
    }

    [Fact]
    public void Extract_HandlesThousandsSeparator()
    {
        var ocr = """
            Ace Hardware
            12/03/2026
            Total     1,234.56
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);
        draft.TotalEgp.Should().Be(1234.56m);
    }

    [Theory]
    [InlineData("15/04/2026", 2026, 4, 15)]
    [InlineData("15-04-2026", 2026, 4, 15)]
    [InlineData("2026-04-15", 2026, 4, 15)]
    [InlineData("15.04.2026", 2026, 4, 15)]
    public void Extract_ParsesAllCommonDateFormats(string dateStr, int y, int m, int d)
    {
        var ocr = $"""
            Some Shop
            Date: {dateStr}
            Total      100.00
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);
        draft.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Fact]
    public void Extract_TwoDigitYear_ResolvesTo2000s()
    {
        var ocr = """
            Some Shop
            15/04/26
            Total      100.00
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);
        draft.Date.Should().Be(new DateOnly(2026, 4, 15));
    }

    [Fact]
    public void Extract_RejectsImplausibleAmounts()
    {
        // Phone numbers, store IDs, item codes can match the .DD pattern;
        // any amount under 0.10 EGP or over 1M EGP is rejected.
        var ocr = """
            Tax ID: 100,000.00
            Phone: 010012345.67
            Total  1500000.00
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);
        // 100,000 is plausible. 1,500,000 is over the cap. 010012345.67
        // is a phone (string len > 1M cap). So 100,000 wins.
        draft.TotalEgp.Should().Be(100_000.00m);
    }

    [Fact]
    public void Extract_SkipsBoilerplateAboveTotal_ForSupplier()
    {
        // The line directly above "Total" is "VAT 14%" which is
        // boilerplate; supplier should be the line further up.
        var ocr = """
            Mc Donalds
            Cairo Mall
            Burger        80.00
            Fries         30.00
            VAT 14%       15.40
            TOTAL        125.40
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);
        // "VAT 14%" + "Fries", "Burger" lines all skip boilerplate
        // or amount filters; we walk back to "Cairo Mall" first.
        draft.SupplierName.Should().NotBe("VAT 14%");
        draft.SupplierName.Should().NotContain("Total");
    }

    [Fact]
    public void Extract_PartialResults_NoteExplainsWhatsMissing()
    {
        var ocr = "Just a name with no numbers at all";
        var draft = ReceiptOcrExtractor.Extract(ocr);

        draft.TotalEgp.Should().BeNull();
        draft.ExtractorNote.Should().Contain("total");
    }

    [Fact]
    public void Extract_RejectsImpossibleDate()
    {
        var ocr = """
            Shop
            32/13/2026
            Total      100.00
            """;
        var draft = ReceiptOcrExtractor.Extract(ocr);
        draft.Date.Should().BeNull();
    }
}
