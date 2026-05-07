using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.SharedKernel;

public class ArabicEnglishTextTests
{
    [Fact]
    public void Construct_StoresBothLanguages()
    {
        var text = new ArabicEnglishText("شركة الاختبار", "Test Company");
        text.Arabic.Should().Be("شركة الاختبار");
        text.English.Should().Be("Test Company");
    }

    [Theory]
    [InlineData(Language.Ar, "شركة الاختبار")]
    [InlineData(Language.En, "Test Company")]
    public void Display_PicksByPreferredLanguage(Language preferred, string expected)
    {
        var text = new ArabicEnglishText("شركة الاختبار", "Test Company");
        text.Display(preferred).Should().Be(expected);
    }

    [Fact]
    public void Display_FallsBackToTheOtherLanguageWhenPreferredIsEmpty()
    {
        // Empty Arabic but English present + preferred=Ar → fallback to English.
        var text = new ArabicEnglishText(string.Empty, "Test Company");
        text.Display(Language.Ar).Should().Be("Test Company");

        // Empty English but Arabic present + preferred=En → fallback to Arabic.
        var text2 = new ArabicEnglishText("شركة", string.Empty);
        text2.Display(Language.En).Should().Be("شركة");
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new ArabicEnglishText("شركة", "Company");
        var b = new ArabicEnglishText("شركة", "Company");
        var c = new ArabicEnglishText("شركة", "Other");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(c);
    }

    [Fact]
    public void EmptyConstants_AreSingletons()
    {
        ArabicEnglishText.Empty.Arabic.Should().BeEmpty();
        ArabicEnglishText.Empty.English.Should().BeEmpty();
    }
}
