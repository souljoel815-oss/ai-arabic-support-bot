using Microsoft.AspNetCore.DataProtection;

namespace EgyptTax.Infrastructure.Settings;

/// <summary>
/// Gux.13 Tab 5 — encrypts the SMTP password before persistence so
/// the plaintext never touches disk. Same DataProtection pattern as
/// MfaSecretProtector — the purpose string namespaces the keyring
/// entry so a key reuse from the cookie/antiforgery side can't
/// collide with an SMTP-password ciphertext.
///
/// On Windows, the underlying keyring is DPAPI-sealed; on Linux
/// (Docker) it falls back to the configured key directory + a
/// machine-bound master key.
/// </summary>
public sealed class SmtpPasswordProtector
{
    private const string Purpose = "EgyptTax.Settings.SmtpPassword.v1";
    private readonly IDataProtector _protector;

    public SmtpPasswordProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose);
    }

    public string Encrypt(string plaintextPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextPassword);
        return _protector.Protect(plaintextPassword);
    }

    public string Decrypt(string encryptedPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPassword);
        return _protector.Unprotect(encryptedPassword);
    }
}
