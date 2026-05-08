using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.SharedKernel;

public class EgyptianTinTests
{
    [Theory]
    [InlineData("123456789")]
    [InlineData("000000001")]
    [InlineData("999999999")]
    public void TryParse_AcceptsExactlyNineDigits(string raw)
    {
        EgyptianTin.TryParse(raw, out var tin).Should().BeTrue();
        tin!.Value.Value.Should().Be(raw);
    }

    [Theory]
    [InlineData("12345678")] // 8 digits
    [InlineData("1234567890")] // 10 digits
    [InlineData("12345678a")] // contains a letter
    [InlineData("12345 6789")] // contains a space
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace
    public void TryParse_RejectsAnythingElse(string raw)
    {
        EgyptianTin.TryParse(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_RejectsNull()
    {
        EgyptianTin.TryParse(null, out _).Should().BeFalse();
    }

    [Fact]
    public void Parse_ThrowsOnInvalid()
    {
        Action act = () => EgyptianTin.Parse("not-a-tin");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = EgyptianTin.Parse("123456789");
        var b = EgyptianTin.Parse("123456789");
        var c = EgyptianTin.Parse("987654321");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(c);
    }

    [Fact]
    public void ImplicitToString_ReturnsCanonicalForm()
    {
        var tin = EgyptianTin.Parse("123456789");
        tin.ToString().Should().Be("123456789");
    }
}
