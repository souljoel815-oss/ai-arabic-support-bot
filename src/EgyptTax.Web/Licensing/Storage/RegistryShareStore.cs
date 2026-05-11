using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EgyptTax.Web.Licensing.Storage;

/// <summary>
/// Stores a Shamir share as a REG_BINARY value under
/// <c>HKCU\Software\DaftarX\Activation</c>. The HKCU hive is
/// per-user — copying the install folder to another machine doesn't
/// carry the registry across, so the share is missing on the copy.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryShareStore : IShareStore
{
    public string Name => "registry";

    private const string KeyPath = @"Software\DaftarX\Activation";
    private const string ValueName = "ShareData";

    public bool TryRead(out byte[] share)
    {
        share = Array.Empty<byte>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key is null) return false;
            if (key.GetValue(ValueName) is byte[] bytes && bytes.Length >= 2)
            {
                share = bytes;
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    public void Write(byte[] share)
    {
        ArgumentNullException.ThrowIfNull(share);
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)!;
        key.SetValue(ValueName, share, RegistryValueKind.Binary);
    }

    public void Erase()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // best-effort
        }
    }
}
