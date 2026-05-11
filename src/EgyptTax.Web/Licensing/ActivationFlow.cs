using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EgyptTax.Web.Licensing.Shamir;
using EgyptTax.Web.Licensing.Storage;

namespace EgyptTax.Web.Licensing;

/// <summary>
/// First-run activation handler. Reads the vendor-supplied
/// <c>license.token</c> JSON envelope, verifies it, generates a
/// fresh master key, splits the key via Shamir's into 3 shares,
/// distributes the shares (DPAPI file + Registry + HWID-derived
/// mask), and writes the AES-encrypted license payload to
/// <c>license.activated</c>.
///
/// On subsequent runs <see cref="LicenseGate"/> reconstructs the
/// master key from any 2 shares and decrypts the activated file.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ActivationFlow
{
    public const string TokenFileName     = "license.token";
    public const string ActivatedFileName = "license.activated";
    public const string Hwid3MaskKey      = "ShareMask3";

    private readonly string _stateDir;
    private readonly DpapiFileShareStore _dpapi;
    private readonly RegistryShareStore _registry;
    private readonly HwidDerivedShareStore _hwid;

    public ActivationFlow(string stateDirectory)
    {
        _stateDir = stateDirectory;
        Directory.CreateDirectory(_stateDir);
        _dpapi = new DpapiFileShareStore();
        _registry = new RegistryShareStore();
        _hwid = new HwidDerivedShareStore(
            maskReader: () => ReadHwidMask(),
            maskWriter: m => WriteHwidMask(m));
    }

    public string TokenPath     => Path.Combine(_stateDir, TokenFileName);
    public string ActivatedPath => Path.Combine(_stateDir, ActivatedFileName);

    /// <summary>
    /// Try to apply a fresh license.token if one exists. Returns
    /// true if activation completed (or was already done); false
    /// when no token + no prior activation are present.
    /// </summary>
    public bool TryActivate()
    {
        if (!File.Exists(TokenPath)) return false;

        var envelopeJson = File.ReadAllText(TokenPath, Encoding.UTF8);
        var hwid = HardwareId.Get();
        var check = LicenseVerifier.Verify(envelopeJson, hwid, DateTime.UtcNow);
        if (!check.IsValid)
        {
            // Bad token — leave it on disk so the operator sees the
            // state and can replace it; don't silently delete.
            return false;
        }

        // Generate a fresh master key — 32 bytes (suitable as both
        // SQLCipher PRAGMA key and AES-256 envelope key).
        var masterKey = RandomNumberGenerator.GetBytes(32);

        // Split into 3 shares (2-of-3 threshold).
        var shares = ShamirSecretSharing.Split(masterKey);

        // Persist 3 shares — note the order is fixed by index:
        //   share[0] (x=1) → DPAPI file
        //   share[1] (x=2) → Registry
        //   share[2] (x=3) → HWID-derived mask
        _dpapi.Write(shares[0]);
        _registry.Write(shares[1]);
        _hwid.Write(shares[2]);

        // Encrypt the verified envelope JSON with the master key —
        // AES-256-GCM, key = first 32 bytes of master, nonce =
        // first 12 bytes of SHA-256(master || "nonce-v1").
        var encrypted = EncryptEnvelope(envelopeJson, masterKey);
        File.WriteAllBytes(ActivatedPath, encrypted);

        // Successful activation — purge the plaintext token.
        try { File.Delete(TokenPath); } catch { /* best-effort */ }

        // Stash the master key for the rest of this process startup
        // (SQLCipher etc. consume it).
        IsLicenseValid.SetMasterKey(masterKey);
        LicenseStatus.RecordValid(check.Payload!, hwid);
        return true;
    }

    /// <summary>
    /// Subsequent-run reconstruction. Reads any 2 of 3 shares,
    /// rebuilds the master key, decrypts the activated envelope,
    /// re-verifies the signature + HWID + expiry. Returns the
    /// payload on success; null on any failure (caller treats null
    /// as "tampered" and routes through the banner middleware).
    /// </summary>
    public LicenseCheckResult ReadActivated()
    {
        if (!File.Exists(ActivatedPath))
            return LicenseCheckResult.Failed(LicenseFailureReason.EnvelopeMissingOrEmpty);

        var collected = new List<byte[]>(3);
        if (_dpapi.TryRead(out var s1)) collected.Add(s1);
        if (_registry.TryRead(out var s2)) collected.Add(s2);
        if (_hwid.TryRead(out var s3)) collected.Add(s3);

        if (collected.Count < ShamirSecretSharing.Threshold)
            return LicenseCheckResult.Failed(LicenseFailureReason.SharesUnreadable);

        byte[] masterKey;
        try
        {
            masterKey = ShamirSecretSharing.Reconstruct(collected);
        }
        catch
        {
            return LicenseCheckResult.Failed(LicenseFailureReason.SharesUnreadable);
        }

        string envelopeJson;
        try
        {
            var encrypted = File.ReadAllBytes(ActivatedPath);
            envelopeJson = DecryptEnvelope(encrypted, masterKey);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(masterKey);
            return LicenseCheckResult.Failed(LicenseFailureReason.DecryptionFailed);
        }

        var check = LicenseVerifier.Verify(envelopeJson, HardwareId.Get(), DateTime.UtcNow);
        if (check.IsValid)
        {
            IsLicenseValid.SetMasterKey(masterKey);
        }
        else
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
        return check;
    }

    // ---------- AES-256-GCM envelope ----------

    private static byte[] EncryptEnvelope(string plaintext, byte[] masterKey)
    {
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var nonce = DeriveNonce(masterKey);
        using var gcm = new AesGcm(masterKey, tagSizeInBytes: 16);
        var ct = new byte[pt.Length];
        var tag = new byte[16];
        gcm.Encrypt(nonce, pt, ct, tag);

        // Wire format: [4-byte ct length][ct][16-byte tag]
        var output = new byte[4 + ct.Length + 16];
        BitConverter.GetBytes(ct.Length).CopyTo(output, 0);
        Buffer.BlockCopy(ct, 0, output, 4, ct.Length);
        Buffer.BlockCopy(tag, 0, output, 4 + ct.Length, 16);
        return output;
    }

    private static string DecryptEnvelope(byte[] encrypted, byte[] masterKey)
    {
        if (encrypted.Length < 4 + 16) throw new InvalidDataException("Envelope too short.");
        var ctLen = BitConverter.ToInt32(encrypted, 0);
        if (ctLen < 0 || 4 + ctLen + 16 > encrypted.Length) throw new InvalidDataException("Envelope length corrupt.");
        var ct = new byte[ctLen];
        var tag = new byte[16];
        Buffer.BlockCopy(encrypted, 4, ct, 0, ctLen);
        Buffer.BlockCopy(encrypted, 4 + ctLen, tag, 0, 16);

        var nonce = DeriveNonce(masterKey);
        using var gcm = new AesGcm(masterKey, tagSizeInBytes: 16);
        var pt = new byte[ctLen];
        gcm.Decrypt(nonce, ct, tag, pt);
        return Encoding.UTF8.GetString(pt);
    }

    private static byte[] DeriveNonce(byte[] masterKey)
    {
        var seed = new byte[masterKey.Length + 8];
        Buffer.BlockCopy(masterKey, 0, seed, 0, masterKey.Length);
        Encoding.UTF8.GetBytes("nonce-v1").CopyTo(seed, masterKey.Length);
        var hash = SHA256.HashData(seed);
        var nonce = new byte[12];
        Buffer.BlockCopy(hash, 0, nonce, 0, 12);
        return nonce;
    }

    // ---------- HWID share mask persistence ----------
    //
    // The mask is plain bytes, NOT encrypted — without the HWID
    // derivation it leaks no secret. Stored alongside the activated
    // envelope so a fresh activation overwrites it cleanly.

    private string MaskPath => Path.Combine(_stateDir, "share3.mask");

    private byte[] ReadHwidMask()
    {
        if (!File.Exists(MaskPath)) return Array.Empty<byte>();
        return File.ReadAllBytes(MaskPath);
    }

    private void WriteHwidMask(byte[] mask)
    {
        File.WriteAllBytes(MaskPath, mask);
    }

    /// <summary>
    /// Wipe everything from disk + registry — used by the
    /// "Deactivate" admin action when a customer transfers their
    /// license to a new machine.
    /// </summary>
    public void EraseAll()
    {
        try { if (File.Exists(ActivatedPath)) File.Delete(ActivatedPath); } catch { }
        try { if (File.Exists(MaskPath)) File.Delete(MaskPath); } catch { }
        _dpapi.Erase();
        _registry.Erase();
        IsLicenseValid.ClearMasterKey();
    }
}
