namespace EgyptTax.Application.Identity;

/// <summary>
/// FR-002 / R-07 — RFC 6238 TOTP port. Production wires the
/// <c>Otp.NET</c>-backed implementation; secrets returned here are plain
/// base32 and the persistence layer encrypts them at rest via a value
/// converter.
/// </summary>
public interface ITotpService
{
    string GenerateSecret();
    string GenerateCode(string secret);
    bool VerifyCode(string secret, string code);
    string BuildProvisioningUri(string accountName, string secret, string issuer);
}
