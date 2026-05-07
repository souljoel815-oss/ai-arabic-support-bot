using System.Security.Cryptography;
using System.Web;
using EgyptTax.Application.Identity;
using OtpNet;

namespace EgyptTax.Infrastructure.Identity;

/// <summary>
/// FR-002 / R-07 — RFC 6238 TOTP via <c>Otp.NET</c>. Generates 160-bit
/// (20-byte) base32-encoded secrets, builds the standard
/// <c>otpauth://</c> provisioning URI for QR enrolment, and verifies
/// six-digit codes with the library's default ±1-step window.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int SecretLengthBytes = 20; // 160-bit per RFC 6238
    private const int DigitsPerCode = 6;

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretLengthBytes);
        return Base32Encoding.ToString(bytes);
    }

    public string GenerateCode(string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var bytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(bytes, totpSize: DigitsPerCode);
        return totp.ComputeTotp();
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code))
        {
            return false;
        }

        if (code.Length != DigitsPerCode || !code.All(char.IsDigit))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Base32Encoding.ToBytes(secret);
        }
        catch (Exception)
        {
            return false;
        }

        var totp = new Totp(bytes, totpSize: DigitsPerCode);
        return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    public string BuildProvisioningUri(string accountName, string secret, string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        var encodedAccount = HttpUtility.UrlEncode(accountName);
        var encodedIssuer = HttpUtility.UrlEncode(issuer);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&digits={DigitsPerCode}";
    }
}
