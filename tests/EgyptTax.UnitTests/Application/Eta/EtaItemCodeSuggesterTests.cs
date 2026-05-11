using EgyptTax.Application.Eta.ItemCodeSuggestions;

namespace EgyptTax.UnitTests.Application.Eta;

/// <summary>
/// G2.1 — covers the in-memory ETA item-code fuzzy matcher.
/// Operates on the bundled corpus (~70 codes), so the assertions
/// are real-corpus assertions, not "for some hypothetical entry".
/// </summary>
public class EtaItemCodeSuggesterTests
{
    [Fact]
    public void Corpus_LoadedFromEmbeddedJson_AndNotEmpty()
    {
        EtaItemCodeSuggester.All.Should().NotBeEmpty();
        EtaItemCodeSuggester.All.Should().Contain(e => e.Code == "10000123" && e.Category == "Electronics");
    }

    [Fact]
    public void Suggest_BlankInput_ReturnsEmpty()
    {
        EtaItemCodeSuggester.Suggest("").Should().BeEmpty();
        EtaItemCodeSuggester.Suggest("   ").Should().BeEmpty();
        EtaItemCodeSuggester.Suggest(null!).Should().BeEmpty();
    }

    [Fact]
    public void Suggest_ExactArabicKeyword_ReturnsTopMatch()
    {
        var s = EtaItemCodeSuggester.Suggest("لاب توب");
        s.Should().NotBeEmpty();
        s[0].Code.Should().Be("10000123"); // laptop
        s[0].Confidence.Should().BeGreaterOrEqualTo(60);
    }

    [Fact]
    public void Suggest_EnglishKeyword_FindsLaptop()
    {
        // "Dell laptop" — 1 hit out of 2 input tokens against "laptop"
        // → ~30 score (50% × 60). Above MinUsefulScore (25) so it
        // surfaces; well below high-confidence (80) so the operator
        // sees the alternative suggestions too.
        var s = EtaItemCodeSuggester.Suggest("Dell laptop");
        s.Should().NotBeEmpty();
        s[0].Code.Should().Be("10000123");
        s[0].Confidence.Should().BeGreaterOrEqualTo(EtaItemCodeSuggester.MinUsefulScore);
    }

    [Fact]
    public void Suggest_PartialArabicSpelling_StillMatches()
    {
        // "كمامة" (mask) vs canonical keyword "كمامة" — exact hit
        var s = EtaItemCodeSuggester.Suggest("كمامة طبية");
        s.Should().NotBeEmpty();
        s[0].Code.Should().Be("10004004");
    }

    [Fact]
    public void Suggest_AccountingService_ReturnsEgsCode()
    {
        var s = EtaItemCodeSuggester.Suggest("خدمات محاسبة");
        s.Should().NotBeEmpty();
        s[0].Code.Should().Be("EGS-SVC-001");
        s[0].Kind.Should().Be("EGS");
    }

    [Fact]
    public void Suggest_NoMatchingTerms_ReturnsEmpty()
    {
        // Nothing in the bundled corpus matches "zzz quantum flux capacitor"
        var s = EtaItemCodeSuggester.Suggest("zzz quantum flux capacitor");
        s.Should().BeEmpty();
    }

    [Fact]
    public void Suggest_RespectsTopN()
    {
        // Use a deliberately-broad term that matches multiple categories.
        var s = EtaItemCodeSuggester.Suggest("paper", topN: 2);
        s.Count.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public void Suggest_OrdersByConfidenceDescending()
    {
        var s = EtaItemCodeSuggester.Suggest("printer toner cartridge", topN: 3);
        s.Should().NotBeEmpty();
        var prev = int.MaxValue;
        foreach (var item in s)
        {
            item.Confidence.Should().BeLessOrEqualTo(prev);
            prev = item.Confidence;
        }
    }

    [Fact]
    public void Suggest_MixedArabicAndEnglishInput_StillMatches()
    {
        // Operator types both languages — should still find "rice"
        var s = EtaItemCodeSuggester.Suggest("أرز white rice");
        s.Should().NotBeEmpty();
        s[0].Code.Should().Be("10001005");
    }

    [Fact]
    public void Suggest_ScoreClamped_0_to_100()
    {
        // Construct an extreme matching input: all keywords for one entry.
        var entry = EtaItemCodeSuggester.All.First(e => e.Code == "10000131"); // smartphone
        var input = string.Join(" ", entry.Ar.Concat(entry.En));
        var s = EtaItemCodeSuggester.Suggest(input);

        s.Should().NotBeEmpty();
        s.Should().AllSatisfy(item =>
        {
            item.Confidence.Should().BeInRange(0, 100);
        });
    }
}
