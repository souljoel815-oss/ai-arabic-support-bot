using EgyptTax.SharedKernel.Localization;

namespace EgyptTax.UnitTests.SharedKernel;

/// <summary>
/// T062 — golden-vector tests for the FR-014 / R-12 Arabic-words
/// converter. Two surfaces are tested:
///   (a) <see cref="ArabicWordsConverter.FromInteger"/> — pure integer
///       conversion (0..999_999_999_999) used for invoice line items
///       and document numbering when localized.
///   (b) <see cref="ArabicWordsConverter.FromEgyptianPounds"/> — the
///       legal-invoice "amount in words" format (FR-014) that
///       appends the EGP currency suffix and the trailing "only and
///       nothing more" closer mandated by Egyptian invoicing
///       conventions.
/// The vectors deliberately cover every transition: ones, teens,
/// tens, mixed tens-with-ones, hundreds (especially the irregular
/// 200/300/800 forms), thousands (singular/dual/3-10/11+ scale word
/// agreement), millions, billions, and the "صفر" zero special case.
/// </summary>
public class ArabicWordsConverterTests
{
    [Theory]
    [InlineData(0L, "صفر")]
    [InlineData(1L, "واحد")]
    [InlineData(2L, "اثنان")]
    [InlineData(3L, "ثلاثة")]
    [InlineData(7L, "سبعة")]
    [InlineData(9L, "تسعة")]
    [InlineData(10L, "عشرة")]
    [InlineData(11L, "أحد عشر")]
    [InlineData(12L, "اثنا عشر")]
    [InlineData(13L, "ثلاثة عشر")]
    [InlineData(15L, "خمسة عشر")]
    [InlineData(19L, "تسعة عشر")]
    [InlineData(20L, "عشرون")]
    [InlineData(21L, "واحد وعشرون")]
    [InlineData(25L, "خمسة وعشرون")]
    [InlineData(30L, "ثلاثون")]
    [InlineData(45L, "خمسة وأربعون")]
    [InlineData(99L, "تسعة وتسعون")]
    [InlineData(100L, "مائة")]
    [InlineData(101L, "مائة وواحد")]
    [InlineData(120L, "مائة وعشرون")]
    [InlineData(200L, "مئتان")]
    [InlineData(213L, "مئتان وثلاثة عشر")]
    [InlineData(300L, "ثلاثمائة")]
    [InlineData(800L, "ثمانمائة")]
    [InlineData(999L, "تسعمائة وتسعة وتسعون")]
    public void FromInteger_OnesTensHundreds_MatchesGoldenVector(long value, string expected)
    {
        ArabicWordsConverter.FromInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1_000L, "ألف")]
    [InlineData(2_000L, "ألفان")]
    [InlineData(3_000L, "ثلاثة آلاف")]
    [InlineData(5_500L, "خمسة آلاف وخمسمائة")]
    [InlineData(10_000L, "عشرة آلاف")]
    [InlineData(11_000L, "أحد عشر ألفا")]
    [InlineData(12_500L, "اثنا عشر ألفا وخمسمائة")]
    [InlineData(100_000L, "مائة ألف")]
    [InlineData(125_000L, "مائة وخمسة وعشرون ألفا")]
    public void FromInteger_Thousands_MatchesGoldenVector(long value, string expected)
    {
        ArabicWordsConverter.FromInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1_000_000L, "مليون")]
    [InlineData(2_000_000L, "مليونان")]
    [InlineData(3_000_000L, "ثلاثة ملايين")]
    [InlineData(1_234_567L, "مليون ومئتان وأربعة وثلاثون ألفا وخمسمائة وسبعة وستون")]
    [InlineData(1_000_000_000L, "مليار")]
    [InlineData(2_000_000_000L, "ملياران")]
    public void FromInteger_MillionsAndBillions_MatchesGoldenVector(long value, string expected)
    {
        ArabicWordsConverter.FromInteger(value).Should().Be(expected);
    }

    [Fact]
    public void FromInteger_Negative_Throws()
    {
        var act = () => ArabicWordsConverter.FromInteger(-1L);
        act.Should()
            .Throw<ArgumentOutOfRangeException>(
                because: "FR-014 amount-in-words is only meaningful for non-negative monetary quantities"
            );
    }

    [Theory]
    [InlineData("0.00", "صفر جنيه مصري فقط لا غير")]
    [InlineData("0.50", "خمسون قرشا فقط لا غير")]
    [InlineData("1.00", "جنيه مصري واحد فقط لا غير")]
    [InlineData("2.00", "جنيهان مصريان فقط لا غير")]
    [InlineData("5.00", "خمسة جنيهات مصرية فقط لا غير")]
    [InlineData("11.00", "أحد عشر جنيها مصريا فقط لا غير")]
    [InlineData("100.50", "مائة جنيها مصريا وخمسون قرشا فقط لا غير")]
    [InlineData("1234.56", "ألف ومئتان وأربعة وثلاثون جنيها مصريا وستة وخمسون قرشا فقط لا غير")]
    public void FromEgyptianPounds_MatchesGoldenVector(string amount, string expected)
    {
        var dec = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        ArabicWordsConverter.FromEgyptianPounds(dec).Should().Be(expected);
    }

    [Fact]
    public void FromEgyptianPounds_RoundsToTwoPlaces_BankersRounding()
    {
        // 0.005 banker-rounds to 0.00 (nearest even); 0.015 banker-rounds
        // to 0.02. These are the cases that distinguish banker's from
        // traditional half-up rounding.
        var halfDownEven = decimal.Parse(
            "0.005",
            System.Globalization.CultureInfo.InvariantCulture
        );
        ArabicWordsConverter
            .FromEgyptianPounds(halfDownEven)
            .Should()
            .Be("صفر جنيه مصري فقط لا غير");

        var halfUpEven = decimal.Parse("0.015", System.Globalization.CultureInfo.InvariantCulture);
        ArabicWordsConverter.FromEgyptianPounds(halfUpEven).Should().Be("قرشان فقط لا غير");
    }

    [Fact]
    public void FromEgyptianPounds_Negative_Throws()
    {
        var act = () => ArabicWordsConverter.FromEgyptianPounds(-0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
