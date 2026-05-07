using System.Text;
using EgyptTax.Application.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace EgyptTax.Infrastructure.Identity;

/// <summary>
/// FR-002 — encrypts the plaintext base32 TOTP secret before persistence
/// using ASP.NET Core <see cref="IDataProtector"/>. The protector's
/// purpose string namespaces the keyring entry so re-using the same
/// data-protection root for cookies, anti-forgery tokens, etc. cannot
/// produce a ciphertext collision. On Windows, the underlying keyring
/// is sealed with DPAPI.
/// </summary>
public sealed class DataProtectionMfaSecretProtector : IMfaSecretProtector
{
    private const string Purpose = "EgyptTax.Identity.MfaSecret.v1";
    private readonly IDataProtector _protector;

    public DataProtectionMfaSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose);
    }

    public byte[] Protect(string plaintextBase32Secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextBase32Secret);
        var plaintext = Encoding.UTF8.GetBytes(plaintextBase32Secret);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(byte[] cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);
        if (cipherText.Length == 0)
        {
            throw new ArgumentException("Ciphertext must be non-empty.", nameof(cipherText));
        }
        var plaintext = _protector.Unprotect(cipherText);
        return Encoding.UTF8.GetString(plaintext);
    }
}
