using OtpNet;

namespace EgyptTax.Portal.Infrastructure.Identity;

/// <summary>
/// T020 per FR-011. Wraps Otp.NET to generate + verify TOTP codes for
/// opt-in MFA. Per-user secret is stored in
/// <see cref="Microsoft.AspNetCore.Identity.IdentityUser{TKey}"/>'s
/// authenticator-key store (rotated via
/// <c>UserManager.GetAuthenticatorKeyAsync</c>) — this service deals
/// only with the deterministic code derivation + verification window.
///
/// Org-level enforcement (FR-011 second clause / T155) lives in
/// <c>OrganisationMfaPolicyMiddleware</c>; this service is the leaf
/// crypto wrapper used by both the per-user opt-in flow and the
/// org-policy enforcement path.
/// </summary>
public interface ITotpMfaService
{
    /// <summary>Generate a fresh 160-bit base32-encoded secret for enrolment.</summary>
    string GenerateSecret();

    /// <summary>otpauth:// provisioning URI consumed by Google Authenticator etc.</summary>
    string BuildProvisioningUri(string secretBase32, string accountEmail, string issuer = "DaftarX");

    /// <summary>Verify a 6-digit code against the secret with a ±30 s window.</summary>
    bool VerifyCode(string secretBase32, string code);
}

internal sealed class TotpMfaService : ITotpMfaService
{
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string BuildProvisioningUri(string secretBase32, string accountEmail, string issuer = "DaftarX")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretBase32);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountEmail);

        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountEmail);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secretBase32}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string secretBase32, string code)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        try
        {
            var key = Base32Encoding.ToBytes(secretBase32);
            var totp = new Totp(key);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch (FormatException)
        {
            // Malformed base32 secret — treat as a failed verification.
            return false;
        }
    }
}
