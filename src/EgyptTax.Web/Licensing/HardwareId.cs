using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace EgyptTax.Web.Licensing;

/// <summary>
/// Per-machine fingerprint derived from CPU + motherboard + primary
/// disk volume serials. SHA-256 over the concatenation, returned
/// as a 16-character upper-hex digest with dashes for readability:
///   D4F1-7A23-9C8E-B051
///
/// Stable across reboots and Windows reinstalls (the underlying
/// hardware doesn't change). NOT stable across motherboard
/// replacement — by design — that's what triggers re-activation.
///
/// Cached on first call so the WMI queries (~150ms) don't run on
/// every license check.
/// </summary>
[SupportedOSPlatform("windows")]
public static class HardwareId
{
    private static readonly object _lock = new();
    private static string? _cached;

    public static string Get()
    {
        if (_cached is not null) return _cached;
        lock (_lock)
        {
            _cached ??= Compute();
            return _cached;
        }
    }

    private static string Compute()
    {
        var cpu = SafeWmiQuery("SELECT ProcessorId FROM Win32_Processor", "ProcessorId");
        var mobo = SafeWmiQuery("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber");
        var disk = SafeWmiQuery(
            "SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID = 'C:'",
            "VolumeSerialNumber");

        // Fall back to the machine GUID if any WMI source is empty —
        // some Hyper-V / VBox guests don't surface ProcessorId.
        if (string.IsNullOrWhiteSpace(cpu)) cpu = SafeMachineGuid();
        if (string.IsNullOrWhiteSpace(mobo)) mobo = Environment.MachineName;

        var raw = $"{cpu}|{mobo}|{disk}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        // Take 8 bytes → 16 hex chars, group as 4-4-4-4 with dashes.
        var hex = Convert.ToHexString(hash, 0, 8);
        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    private static string SafeWmiQuery(string query, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject obj in searcher.Get())
            {
                var value = obj[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
        }
        catch
        {
            // WMI queries can throw on locked-down machines or inside
            // some sandboxes — fall through to fallback.
        }
        return "";
    }

    private static string SafeMachineGuid()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Returns the 32-byte SHA-256 of the displayed HWID — used as
    /// the seed for the third Shamir share so it doesn't need
    /// physical storage anywhere.
    /// </summary>
    public static byte[] DerivedKeyMaterial()
    {
        var id = Get();
        return SHA256.HashData(Encoding.UTF8.GetBytes("daftarx-hwid-share|" + id));
    }
}
