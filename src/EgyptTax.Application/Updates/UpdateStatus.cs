using System.Reflection;

namespace EgyptTax.Application.Updates;

/// <summary>
/// G4.1 — process-wide singleton holding the latest update-check
/// result. Written once per day by <c>UpdateCheckJob</c>; read by
/// the topbar banner on every page render. Same volatile pattern
/// as <c>LicenseStatus</c>.
///
/// <see cref="CurrentVersion"/> is read once from the executing
/// assembly at first access (this assembly's version is always
/// in lock-step with the Web binary's because Web depends on
/// Application).
/// </summary>
public static class UpdateStatus
{
    private static readonly Lazy<Version> _currentVersionLazy = new(() =>
    {
        var asm = typeof(UpdateStatus).Assembly;
        var version = asm.GetName().Version ?? new Version(0, 0, 0, 0);
        // Strip the trailing build/revision noise — reading "1.2.0"
        // beats "1.2.0.0" in UI strings.
        return new Version(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);
    });

    private static UpdateManifest? _latest;
    private static DateTime? _lastCheckedUtc;
    private static string? _lastCheckError;

    public static Version CurrentVersion => _currentVersionLazy.Value;

    public static UpdateManifest? Latest => _latest;

    public static DateTime? LastCheckedUtc => _lastCheckedUtc;

    public static string? LastCheckError => _lastCheckError;

    /// <summary>True when a strictly-newer version is available.</summary>
    public static bool UpdateAvailable
    {
        get
        {
            if (_latest is null) return false;
            return Version.TryParse(_latest.LatestVersion, out var remote)
                && remote > CurrentVersion;
        }
    }

    public static void RecordSuccess(UpdateManifest manifest, DateTime checkedAtUtc)
    {
        _latest = manifest;
        _lastCheckedUtc = checkedAtUtc;
        _lastCheckError = null;
    }

    public static void RecordFailure(string error, DateTime checkedAtUtc)
    {
        _lastCheckError = error;
        _lastCheckedUtc = checkedAtUtc;
        // Keep the previous _latest — a transient failure shouldn't
        // hide an update that we already know about from yesterday.
    }
}
