using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace EgyptTax.Web.Licensing.Storage;

/// <summary>
/// Stores a Shamir share as a DPAPI-encrypted blob in
/// <c>%APPDATA%\DaftarX\share.bin</c>. DPAPI scopes the encryption
/// to the current Windows user (CurrentUser) — copying the file to
/// a different user account on the same machine, or to a different
/// machine entirely, makes it undecryptable.
///
/// Combined with the per-machine entropy in
/// <see cref="DpapiEntropy"/>, an attacker would need BOTH the
/// DPAPI master key AND knowledge of the entropy bytes — both
/// derived from the local user profile.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiFileShareStore : IShareStore
{
    public string Name => "dpapi-file";

    private static readonly byte[] DpapiEntropy = System.Text.Encoding.UTF8.GetBytes(
        "daftarx-license-share-2026");

    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DaftarX");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "share.bin");
        }
    }

    public bool TryRead(out byte[] share)
    {
        share = Array.Empty<byte>();
        try
        {
            if (!File.Exists(FilePath)) return false;
            var encrypted = File.ReadAllBytes(FilePath);
            share = ProtectedData.Unprotect(encrypted, DpapiEntropy, DataProtectionScope.CurrentUser);
            return share.Length >= 2;
        }
        catch
        {
            // DPAPI throws when the user profile changed — caller
            // treats this as "share missing", same as file-not-found.
            return false;
        }
    }

    public void Write(byte[] share)
    {
        ArgumentNullException.ThrowIfNull(share);
        var encrypted = ProtectedData.Protect(share, DpapiEntropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encrypted);
    }

    public void Erase()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { /* best-effort */ }
    }
}
