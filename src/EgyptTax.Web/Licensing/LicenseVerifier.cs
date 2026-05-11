using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace EgyptTax.Web.Licensing;

/// <summary>
/// Pure-managed Ed25519 verifier (BouncyCastle.Cryptography).
/// Hands back a typed reason on failure so the caller can render
/// an actionable banner instead of a generic "license invalid".
/// </summary>
public static class LicenseVerifier
{
    public static LicenseCheckResult Verify(
        string envelopeJson,
        string currentHwid,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(envelopeJson))
            return LicenseCheckResult.Failed(LicenseFailureReason.EnvelopeMissingOrEmpty);

        LicenseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(envelopeJson);
        }
        catch
        {
            return LicenseCheckResult.Failed(LicenseFailureReason.EnvelopeMalformed);
        }
        if (envelope is null || envelope.Payload is null)
            return LicenseCheckResult.Failed(LicenseFailureReason.EnvelopeMalformed);

        byte[] sig;
        try
        {
            sig = Convert.FromBase64String(envelope.Signature ?? "");
        }
        catch
        {
            return LicenseCheckResult.Failed(LicenseFailureReason.SignatureMalformed);
        }
        if (sig.Length != 64)
            return LicenseCheckResult.Failed(LicenseFailureReason.SignatureMalformed);

        var canonical = LicensePayloadSerializer.CanonicalBytes(envelope.Payload);

        var pubKey = new Ed25519PublicKeyParameters(LicensePublicKey.Bytes, 0);
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, pubKey);
        verifier.BlockUpdate(canonical, 0, canonical.Length);
        if (!verifier.VerifySignature(sig))
            return LicenseCheckResult.Failed(LicenseFailureReason.SignatureInvalid, envelope.Payload);

        // HWID match — case + dash insensitive comparison so the
        // operator can paste with or without the readability dashes.
        var normCurrent = Normalise(currentHwid);
        var normLicense = Normalise(envelope.Payload.Hwid);
        if (!string.Equals(normCurrent, normLicense, StringComparison.Ordinal))
            return LicenseCheckResult.Failed(LicenseFailureReason.HwidMismatch, envelope.Payload);

        if (envelope.Payload.ExpiresAtUtc < nowUtc)
            return LicenseCheckResult.Failed(LicenseFailureReason.Expired, envelope.Payload);

        if (envelope.Payload.Version != 1)
            return LicenseCheckResult.Failed(LicenseFailureReason.UnsupportedVersion, envelope.Payload);

        return LicenseCheckResult.Ok(envelope.Payload);
    }

    private static string Normalise(string id) =>
        new string(id.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray())
            .ToUpperInvariant();
}

public sealed record LicenseCheckResult(
    bool IsValid,
    LicenseFailureReason FailureReason,
    LicensePayload? Payload)
{
    public static LicenseCheckResult Ok(LicensePayload p) =>
        new(true, LicenseFailureReason.None, p);
    public static LicenseCheckResult Failed(LicenseFailureReason r, LicensePayload? p = null) =>
        new(false, r, p);
}

public enum LicenseFailureReason
{
    None,
    EnvelopeMissingOrEmpty,
    EnvelopeMalformed,
    SignatureMalformed,
    SignatureInvalid,
    HwidMismatch,
    Expired,
    UnsupportedVersion,
    DecryptionFailed,
    SharesUnreadable,
}
