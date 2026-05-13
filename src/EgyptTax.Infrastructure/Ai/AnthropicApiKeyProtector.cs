using Microsoft.AspNetCore.DataProtection;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// M-phase — encrypts the Anthropic API key at rest. Same
/// DataProtection pattern as <c>SmtpPasswordProtector</c>;
/// distinct purpose string so the keyring entries don't collide.
/// </summary>
public sealed class AnthropicApiKeyProtector
{
    private const string Purpose = "EgyptTax.Settings.AnthropicApiKey.v1";
    private readonly IDataProtector _protector;

    public AnthropicApiKeyProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose);
    }

    public string Encrypt(string plaintextKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextKey);
        return _protector.Protect(plaintextKey);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);
        return _protector.Unprotect(ciphertext);
    }
}
