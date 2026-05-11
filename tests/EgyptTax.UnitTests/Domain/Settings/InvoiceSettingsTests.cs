using EgyptTax.Domain.Settings;

namespace EgyptTax.UnitTests.Domain.Settings;

/// <summary>
/// Gux.13 — covers <see cref="InvoiceSettings"/> entity invariants.
/// The FR-037 period-lock guard for the Next Number override is
/// upstream in <c>UpdateInvoiceNumberHandler</c> (the entity can't
/// see other aggregates); these tests cover what the entity itself
/// is responsible for.
/// </summary>
public class InvoiceSettingsTests
{
    [Fact]
    public void CreateDefault_HasExpectedDefaults()
    {
        var s = InvoiceSettings.CreateDefault();

        s.TemplateId.Should().Be("classic");
        s.Language.Should().Be(InvoiceLanguageMode.ArabicOnly);
        s.Prefix.Should().Be("INV-");
        s.NextNumber.Should().Be(1);
        s.DefaultPaymentTermsDays.Should().Be(30);
        s.FooterNotes.Should().BeNull();
        s.ShowQrCode.Should().BeTrue();
        s.ShowLogo.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateTemplate_RejectsBlank(string? bad)
    {
        var s = InvoiceSettings.CreateDefault();
        var act = () => s.UpdateTemplate(bad!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateTemplate_TrimsWhitespace()
    {
        var s = InvoiceSettings.CreateDefault();
        s.UpdateTemplate("  modern  ");
        s.TemplateId.Should().Be("modern");
    }

    [Fact]
    public void UpdatePrefix_AcceptsEmpty()
    {
        // Some operators want raw numbers without a prefix.
        var s = InvoiceSettings.CreateDefault();
        s.UpdatePrefix("");
        s.Prefix.Should().Be("");
    }

    [Fact]
    public void UpdatePrefix_TrimsWhitespace()
    {
        var s = InvoiceSettings.CreateDefault();
        s.UpdatePrefix("  CUST-  ");
        s.Prefix.Should().Be("CUST-");
    }

    [Theory]
    [InlineData(0, true)]   // 0 is invalid (must be ≥ 1)
    [InlineData(-1, true)]
    [InlineData(1, false)]
    [InlineData(999_999, false)]
    public void UpdateNextNumber_EnforcesPositive(int value, bool shouldThrow)
    {
        var s = InvoiceSettings.CreateDefault();
        var act = () => s.UpdateNextNumber(value);
        if (shouldThrow) act.Should().Throw<ArgumentOutOfRangeException>();
        else act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]    // 0 is "due immediately"
    [InlineData(30, false)]
    [InlineData(365, false)]
    [InlineData(366, true)]   // > 1 year is suspicious
    public void UpdatePaymentTerms_EnforcesRange(int value, bool shouldThrow)
    {
        var s = InvoiceSettings.CreateDefault();
        var act = () => s.UpdatePaymentTerms(value);
        if (shouldThrow) act.Should().Throw<ArgumentOutOfRangeException>();
        else act.Should().NotThrow();
    }

    [Fact]
    public void UpdateFooterNotes_TrimsAndNullsBlank()
    {
        var s = InvoiceSettings.CreateDefault();

        s.UpdateFooterNotes("  Bank: Alex 1234  ");
        s.FooterNotes.Should().Be("Bank: Alex 1234");

        s.UpdateFooterNotes("   ");
        s.FooterNotes.Should().BeNull();

        s.UpdateFooterNotes(null);
        s.FooterNotes.Should().BeNull();
    }

    [Fact]
    public void UpdateShowQrCode_TogglesIndependentlyOfLogo()
    {
        var s = InvoiceSettings.CreateDefault();
        s.UpdateShowQrCode(false);
        s.ShowQrCode.Should().BeFalse();
        s.ShowLogo.Should().BeTrue(); // unchanged
    }

    [Fact]
    public void UpdateLanguage_SwitchesBetweenModes()
    {
        var s = InvoiceSettings.CreateDefault();
        s.UpdateLanguage(InvoiceLanguageMode.Bilingual);
        s.Language.Should().Be(InvoiceLanguageMode.Bilingual);

        s.UpdateLanguage(InvoiceLanguageMode.ArabicOnly);
        s.Language.Should().Be(InvoiceLanguageMode.ArabicOnly);
    }
}
