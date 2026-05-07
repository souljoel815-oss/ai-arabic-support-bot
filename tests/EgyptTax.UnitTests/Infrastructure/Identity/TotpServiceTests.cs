using EgyptTax.Infrastructure.Identity;

namespace EgyptTax.UnitTests.Infrastructure.Identity;

public class TotpServiceTests
{
    [Fact]
    public void GenerateSecret_ReturnsBase32Encoded160BitSecret()
    {
        var sut = new TotpService();

        var secret = sut.GenerateSecret();

        // RFC 4648 base32 of 20 bytes = 32 chars (no padding when length divisible by 8 bits/5).
        secret.Should().HaveLength(32);
        secret.Should().MatchRegex("^[A-Z2-7]+$");
    }

    [Fact]
    public void GenerateSecret_IsRandomPerCall()
    {
        var sut = new TotpService();

        var a = sut.GenerateSecret();
        var b = sut.GenerateSecret();

        a.Should().NotBe(b);
    }

    [Fact]
    public void BuildProvisioningUri_FollowsKeyUriFormat()
    {
        var sut = new TotpService();
        var secret = sut.GenerateSecret();

        var uri = sut.BuildProvisioningUri("user@test.local", secret, "EgyptTax");

        // Per Google Authenticator KeyUriFormat:
        // otpauth://totp/Issuer:Account?secret=...&issuer=Issuer
        // Special chars in the label (here, '@') MUST be percent-encoded.
        uri.Should().StartWith("otpauth://totp/EgyptTax:user%40test.local?");
        uri.Should().Contain($"secret={secret}");
        uri.Should().Contain("issuer=EgyptTax");
        uri.Should().Contain("digits=6");
    }

    [Fact]
    public void VerifyCode_AcceptsCurrentCode()
    {
        var sut = new TotpService();
        var secret = sut.GenerateSecret();
        var currentCode = sut.GenerateCode(secret);

        sut.VerifyCode(secret, currentCode).Should().BeTrue();
    }

    [Fact]
    public void VerifyCode_RejectsGarbage()
    {
        var sut = new TotpService();
        var secret = sut.GenerateSecret();

        sut.VerifyCode(secret, "000000").Should().BeFalse();
        sut.VerifyCode(secret, "abc123").Should().BeFalse();
        sut.VerifyCode(secret, string.Empty).Should().BeFalse();
    }
}
