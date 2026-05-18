using System.Text.Json;
using EgyptTax.Portal.Application.Licences;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace EgyptTax.Portal.Infrastructure.Licences;

/// <summary>
/// T028. BouncyCastle Ed25519 implementation of <see cref="ILicenceSigningService"/>.
/// Reads the vendor's Ed25519 keypair from a JSON file on disk (path comes
/// from <see cref="LicenceSigningOptions.VendorKeysPath"/>). The file format
/// matches the on-prem product's <c>vendor-keys.json</c>:
/// <code>
/// { "publicKeyBase64": "...", "privateKeyBase64": "..." }
/// </code>
/// Public + private keys are raw 32-byte Ed25519 keys (per BouncyCastle's
/// <see cref="Ed25519PrivateKeyParameters"/> wire format), base64-encoded.
/// </summary>
internal sealed class Ed25519LicenceSigningService : ILicenceSigningService, ILicenceSignatureVerifier
{
    private readonly LicenceSigningOptions _options;
    private readonly ILogger<Ed25519LicenceSigningService> _logger;
    private readonly object _keyLoadLock = new();
    private Ed25519PrivateKeyParameters? _cachedPrivateKey;
    private Ed25519PublicKeyParameters? _cachedPublicKey;

    public Ed25519LicenceSigningService(
        IOptions<LicenceSigningOptions> options,
        ILogger<Ed25519LicenceSigningService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<LicenceEnvelopeDto> SignAsync(LicencePayloadDto payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var (privateKey, _) = LoadKeys();

        var canonical = LicencePayloadCanonicalSerializer.CanonicalBytes(payload);
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privateKey);
        signer.BlockUpdate(canonical, 0, canonical.Length);
        var signature = signer.GenerateSignature();

        var envelope = new LicenceEnvelopeDto(payload, Convert.ToBase64String(signature));

        _logger.LogInformation(
            "Signed licence for HWID {Hwid} edition {Edition} expiring {ExpiresAtUtc:O}",
            payload.Hwid,
            payload.Edition,
            payload.ExpiresAtUtc);

        return Task.FromResult(envelope);
    }

    public bool Verify(LicenceEnvelopeDto envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var (_, publicKey) = LoadKeys();

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(envelope.Signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var canonical = LicencePayloadCanonicalSerializer.CanonicalBytes(envelope.Payload);
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, publicKey);
        verifier.BlockUpdate(canonical, 0, canonical.Length);
        return verifier.VerifySignature(signature);
    }

    private (Ed25519PrivateKeyParameters PrivateKey, Ed25519PublicKeyParameters PublicKey) LoadKeys()
    {
        if (_cachedPrivateKey is { } cachedPrivate && _cachedPublicKey is { } cachedPublic)
        {
            return (cachedPrivate, cachedPublic);
        }

        lock (_keyLoadLock)
        {
            if (_cachedPrivateKey is { } privateAfterLock && _cachedPublicKey is { } publicAfterLock)
            {
                return (privateAfterLock, publicAfterLock);
            }

            var path = _options.VendorKeysPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    "LicenceSigning:VendorKeysPath is not configured. Set it to the absolute path of vendor-keys.json before signing licences.");
            }
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Vendor keypair file not found at '{path}'. Pull it from the vendor's password manager and drop it at this path.",
                    path);
            }

            var json = File.ReadAllText(path);
            var keys = JsonSerializer.Deserialize<VendorKeyFile>(json)
                ?? throw new InvalidOperationException($"Could not parse vendor keypair file at '{path}'.");

            if (string.IsNullOrWhiteSpace(keys.PrivateKeyBase64) || string.IsNullOrWhiteSpace(keys.PublicKeyBase64))
            {
                throw new InvalidOperationException(
                    $"Vendor keypair file at '{path}' is missing publicKeyBase64 or privateKeyBase64 fields.");
            }

            var privateBytes = Convert.FromBase64String(keys.PrivateKeyBase64);
            var publicBytes = Convert.FromBase64String(keys.PublicKeyBase64);

            _cachedPrivateKey = new Ed25519PrivateKeyParameters(privateBytes, 0);
            _cachedPublicKey = new Ed25519PublicKeyParameters(publicBytes, 0);
            return (_cachedPrivateKey, _cachedPublicKey);
        }
    }

    private sealed class VendorKeyFile
    {
        [System.Text.Json.Serialization.JsonPropertyName("publicKeyBase64")]
        public string? PublicKeyBase64 { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("privateKeyBase64")]
        public string? PrivateKeyBase64 { get; set; }
    }
}

public sealed class LicenceSigningOptions
{
    public string VendorKeysPath { get; set; } = "vendor-keys.json";
}
