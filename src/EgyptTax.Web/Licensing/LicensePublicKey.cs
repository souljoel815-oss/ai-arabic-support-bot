namespace EgyptTax.Web.Licensing;

/// <summary>
/// Hardcoded vendor-side Ed25519 PUBLIC key. Used to verify license
/// envelope signatures locally (no network calls).
///
/// The matching PRIVATE key MUST stay on the vendor's offline
/// signing machine — never in the customer-shipped binary, never in
/// source control, never on a developer laptop with network access.
///
/// Generate a fresh keypair via:
///   EgyptTax.Web.exe license-keygen
/// → produces vendor-keys.json with publicKeyHex + privateKeyHex.
/// Replace <see cref="HexPublicKey"/> below with the publicKeyHex
/// value, commit, redistribute the customer build. The previous
/// public key becomes invalid for new licenses; existing customer
/// installs continue working with the previous key bundled into
/// their already-installed binary.
///
/// NEVER commit a privateKeyHex to source control. NEVER share it.
/// Loss of the private key = inability to issue new licenses (no
/// disaster — re-keygen + re-bundle the new public key).
/// Compromise of the private key = pirates can issue valid
/// licenses (real disaster — emergency keypair rotation required).
/// </summary>
public static class LicensePublicKey
{
    /// <summary>
    /// 32-byte Ed25519 public key, hex-encoded. Replace with the
    /// output of `EgyptTax.Web.exe license-keygen` before shipping
    /// to a real customer. The placeholder below is a publicly known
    /// test vector — installs with this key cannot verify any
    /// real license and will refuse to start.
    /// </summary>
    public const string HexPublicKey =
        "0535E8655A1304F28FA1779A8971883F9F1F9B63C3B76696FA692550D9DB5707";

    public static byte[] Bytes => Convert.FromHexString(HexPublicKey);
}
