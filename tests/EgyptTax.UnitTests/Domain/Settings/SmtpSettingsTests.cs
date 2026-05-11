using EgyptTax.Domain.Settings;

namespace EgyptTax.UnitTests.Domain.Settings;

/// <summary>
/// Gux.13 — covers <see cref="SmtpSettings"/> entity invariants.
/// Encrypt/decrypt of the password lives in
/// <c>SmtpPasswordProtector</c> (DataProtection wrapper); these
/// tests cover what the entity itself enforces.
/// </summary>
public class SmtpSettingsTests
{
    [Fact]
    public void CreateDefault_StartsInMailClientMode()
    {
        var s = SmtpSettings.CreateDefault();

        s.SendMethod.Should().Be(EmailSendMethod.MailClient);
        s.Server.Should().BeNull();
        s.Port.Should().Be(587);
        s.UseTls.Should().BeTrue();
    }

    [Fact]
    public void UseDirectSmtp_PopulatesAllFields()
    {
        var s = SmtpSettings.CreateDefault();
        s.UseDirectSmtp(
            server: "smtp.gmail.com",
            port: 587,
            username: "info@example.com",
            encryptedPassword: "ciphertext-blob",
            fromAddress: "info@example.com",
            fromName: "Test Co",
            useTls: true);

        s.SendMethod.Should().Be(EmailSendMethod.DirectSmtp);
        s.Server.Should().Be("smtp.gmail.com");
        s.Port.Should().Be(587);
        s.Username.Should().Be("info@example.com");
        s.EncryptedPassword.Should().Be("ciphertext-blob");
        s.FromAddress.Should().Be("info@example.com");
        s.FromName.Should().Be("Test Co");
        s.UseTls.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "user", "info@example.com")]      // blank server
    [InlineData("smtp.gmail.com", "", "info@example.com")] // blank user
    [InlineData("smtp.gmail.com", "user", "")]            // blank from
    public void UseDirectSmtp_RejectsBlankRequiredFields(string server, string user, string from)
    {
        var s = SmtpSettings.CreateDefault();
        var act = () => s.UseDirectSmtp(server, 587, user, "ct", from, "Name", true);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(70000)]
    public void UseDirectSmtp_RejectsOutOfRangePort(int badPort)
    {
        var s = SmtpSettings.CreateDefault();
        var act = () => s.UseDirectSmtp("smtp.x.com", badPort, "u", "ct", "x@x.com", "X", true);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UseMailClient_DoesNotWipeSavedSmtpFields()
    {
        // Operator may toggle MailClient ↔ DirectSmtp; switching back
        // shouldn't lose the previously-saved SMTP config.
        var s = SmtpSettings.CreateDefault();
        s.UseDirectSmtp("smtp.x.com", 587, "u", "ct", "x@x.com", "X", true);
        s.UseMailClient();

        s.SendMethod.Should().Be(EmailSendMethod.MailClient);
        s.Server.Should().Be("smtp.x.com");          // preserved
        s.EncryptedPassword.Should().Be("ct");        // preserved
    }

    [Fact]
    public void UseDirectSmtp_TrimsWhitespace()
    {
        var s = SmtpSettings.CreateDefault();
        s.UseDirectSmtp("  smtp.x.com  ", 587, "  user  ", "ct", "  x@x.com  ", "  Name  ", true);

        s.Server.Should().Be("smtp.x.com");
        s.Username.Should().Be("user");
        s.FromAddress.Should().Be("x@x.com");
        s.FromName.Should().Be("Name");
    }

    [Fact]
    public void UseDirectSmtp_DefaultsFromNameToFromAddressWhenBlank()
    {
        var s = SmtpSettings.CreateDefault();
        s.UseDirectSmtp("smtp.x.com", 587, "u", "ct", "info@example.com", "", true);

        s.FromName.Should().Be("info@example.com");
    }
}
