namespace EgyptTax.Application.Identity;

/// <summary>
/// Encrypts the plaintext TOTP secret before it is persisted on
/// <c>User.MfaSecretEncrypted</c> per data-model A1. Production wires
/// the ASP.NET Core DataProtection-backed implementation (which on
/// Windows uses DPAPI under the hood for the master key); tests can
/// supply an ephemeral provider so the keyring lives in memory.
/// </summary>
public interface IMfaSecretProtector
{
    byte[] Protect(string plaintextBase32Secret);
    string Unprotect(byte[] cipherText);
}
