using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace EgyptTax.Web.Licensing.Storage;

/// <summary>
/// The third Shamir share is NEVER stored at rest — it's derived
/// deterministically from the current machine's HWID. On the same
/// machine the derivation always produces the same share; on a
/// different machine the HWID differs and the derived share doesn't
/// match the polynomial the secret was split with, so reconstruction
/// silently produces garbage.
///
/// Because the share id (x-coordinate) is fixed at 3 in
/// <see cref="ShamirSecretSharing.Split"/>, we need to:
///   1. Use HWID-derived bytes ONLY for the y-coordinate values that
///      were assigned to share 3 at split time.
///   2. Persist the COMPLEMENT (XOR mask) somewhere visible so this
///      store can recover the original y-bytes from
///      <c>HwidBytes XOR Mask</c>.
///
/// The mask is stored in plain bytes alongside the encrypted license
/// blob — knowing the mask without knowing the HWID is useless,
/// because the third-share y-bytes themselves carry no information
/// about the secret unless combined with another share.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HwidDerivedShareStore : IShareStore
{
    public string Name => "hwid-derived";

    private readonly Func<byte[]> _maskReader;
    private readonly Action<byte[]> _maskWriter;

    public HwidDerivedShareStore(Func<byte[]> maskReader, Action<byte[]> maskWriter)
    {
        _maskReader = maskReader;
        _maskWriter = maskWriter;
    }

    public bool TryRead(out byte[] share)
    {
        share = Array.Empty<byte>();
        byte[] mask;
        try
        {
            mask = _maskReader();
        }
        catch
        {
            return false;
        }
        if (mask.Length < 2) return false;

        // mask[0] is the share id (always 3 by construction).
        var derivedY = DeriveYBytes(mask.Length - 1);
        share = new byte[mask.Length];
        share[0] = mask[0];
        for (var i = 1; i < mask.Length; i++)
        {
            share[i] = (byte)(mask[i] ^ derivedY[i - 1]);
        }
        return true;
    }

    public void Write(byte[] share)
    {
        ArgumentNullException.ThrowIfNull(share);
        if (share.Length < 2) throw new ArgumentException("Share too short.", nameof(share));
        var derivedY = DeriveYBytes(share.Length - 1);
        var mask = new byte[share.Length];
        mask[0] = share[0];
        for (var i = 1; i < share.Length; i++)
        {
            mask[i] = (byte)(share[i] ^ derivedY[i - 1]);
        }
        _maskWriter(mask);
    }

    public void Erase()
    {
        // Mask lives where the writer puts it; cleanup happens via
        // the hosting ActivationFlow when it tears down the
        // license.activated file.
    }

    /// <summary>
    /// Stretch the HWID-derived 32-byte seed into <paramref name="length"/>
    /// bytes via HKDF-SHA256. Constant-output for a given HWID.
    /// </summary>
    private static byte[] DeriveYBytes(int length)
    {
        var seed = HardwareId.DerivedKeyMaterial();
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: seed,
            outputLength: length,
            salt: System.Text.Encoding.UTF8.GetBytes("daftarx-hwid-y"),
            info: System.Text.Encoding.UTF8.GetBytes($"share3-len-{length}"));
    }
}
