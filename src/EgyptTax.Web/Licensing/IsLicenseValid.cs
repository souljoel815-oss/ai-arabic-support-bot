namespace EgyptTax.Web.Licensing;

/// <summary>
/// Honeypot. Returns true only when the boot-time gate succeeded.
/// Scattered call sites (PDF render, ETA submit, post handlers,
/// inspection bundle generator) call this on every operation. A
/// cracker who patches the boot-time gate to "always succeed" still
/// has to find and patch every honeypot site OR risk silent state
/// drift the moment any operation runs against an unlicensed install.
///
/// The companion <see cref="MasterKey"/> property holds the
/// reconstructed Shamir master key so SQLCipher and the audit-chain
/// HMAC can pull from a single source. Cleared in
/// <see cref="ClearMasterKey"/> after consumers cache derived keys.
/// </summary>
public static class IsLicenseValid
{
    public static bool Value => LicenseStatus.IsLicensed;

    private static byte[]? _masterKey;
    public static byte[]? MasterKey => _masterKey;

    internal static void SetMasterKey(byte[] key) => _masterKey = key;
    internal static void ClearMasterKey()
    {
        if (_masterKey is not null)
        {
            CryptographicOperations.ZeroMemory(_masterKey);
            _masterKey = null;
        }
    }

    /// <summary>
    /// Throws when called against a non-active license. Use at the
    /// top of any operation that produces customer-visible state
    /// (invoice PDF, ETA submission, exported ZIP, audit append).
    /// </summary>
    public static void EnsureLicensed(string callSite = "")
    {
        if (LicenseStatus.IsLicensed) return;
        throw new LicenseGateException(
            $"License gate not active (state={LicenseStatus.State}). Call site: {callSite}");
    }
}

public sealed class LicenseGateException : Exception
{
    public LicenseGateException(string message) : base(message) { }
}

internal static class CryptographicOperations
{
    // Slim shim for older targets — .NET 8 has the real API.
    public static void ZeroMemory(byte[] buffer)
        => System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer);
}
