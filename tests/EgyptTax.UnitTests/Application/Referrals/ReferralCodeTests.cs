using EgyptTax.Application.Referrals;

namespace EgyptTax.UnitTests.Application.Referrals;

/// <summary>
/// G4.3 — covers the deterministic referral-code derivation in
/// <see cref="ReferralCode"/> and the format-recovery helpers used
/// by the redemption form.
/// </summary>
public class ReferralCodeTests
{
    [Fact]
    public void Derive_IsDeterministic_ForSameHwid()
    {
        var a = ReferralCode.Derive("HWID-ABC-123");
        var b = ReferralCode.Derive("HWID-ABC-123");
        a.Should().Be(b);
    }

    [Fact]
    public void Derive_DiffersBetweenHwids()
    {
        var a = ReferralCode.Derive("HWID-ABC-123");
        var b = ReferralCode.Derive("HWID-XYZ-789");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Derive_IsCaseInsensitive_OnInput()
    {
        var a = ReferralCode.Derive("hwid-abc-123");
        var b = ReferralCode.Derive("HWID-ABC-123");
        a.Should().Be(b);
    }

    [Fact]
    public void Derive_HasDashSeparatedShape()
    {
        var code = ReferralCode.Derive("any-hwid");
        code.Should().HaveLength(9);
        code[4].Should().Be('-');
    }

    [Fact]
    public void Derive_OmitsAmbiguousGlyphs()
    {
        // Sample a wide set of HWIDs; none of the produced codes
        // should contain 0/O/1/I/L (phone-dictation hostile).
        var bad = new[] { '0', 'O', '1', 'I', 'L' };
        for (int i = 0; i < 100; i++)
        {
            var code = ReferralCode.Derive($"hwid-{i:D3}");
            code.Should().NotContainAny(bad.Select(c => c.ToString()));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Derive_RejectsBlankHwid(string? bad)
    {
        var act = () => ReferralCode.Derive(bad!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("AB23-XY78", true)]   // canonical
    [InlineData("ab23-xy78", true)]   // case-insensitive shape check
    [InlineData("AB23XY78", false)]   // missing dash
    [InlineData("AB23-XY7", false)]   // too short
    [InlineData("AB23-XY78A", false)] // too long
    [InlineData("ABO3-XY78", false)]  // contains forbidden glyph 'O'
    [InlineData("AB23-XY7L", false)]  // contains forbidden glyph 'L'
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidShape_AcceptsOnlyCanonicalForm(string? input, bool expected)
    {
        ReferralCode.IsValidShape(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("ab23xy78", "AB23-XY78")]
    [InlineData("AB23 XY78", "AB23-XY78")]
    [InlineData("AB23-XY78", "AB23-XY78")]
    [InlineData("ab23-xy78  ", "AB23-XY78")]
    public void TryNormalize_RecoversCanonicalForm(string input, string expected)
    {
        ReferralCode.TryNormalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]    // < 8 alphabet chars
    [InlineData("0OIL0OIL")]     // forbidden glyphs only — yields empty
    public void TryNormalize_ReturnsNull_ForInvalidInput(string? input)
    {
        ReferralCode.TryNormalize(input).Should().BeNull();
    }
}
