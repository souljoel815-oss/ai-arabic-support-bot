using System.Reflection;

namespace EgyptTax.Web.Mobile;

/// <summary>
/// T027 per specs/009-android-app/tasks.md + research §17.
///
/// Bound to the <c>MobileVersionConfig</c> section in
/// <c>appsettings.json</c> via <c>builder.Services.Configure</c> in
/// <c>Program.cs</c> (T029). Consumed by
/// <c>MeFeaturesEndpoint</c> (T104, US5) which returns the trio
/// (<see cref="ServerVersion"/>, <see cref="MinAppVersion"/>,
/// <see cref="LatestKnownAppVersion"/>) so the Android shell can run
/// the bidirectional version-gate (FR-019) + the side-load update
/// banner (FR-020) on every foreground.
///
/// Uses <c>IOptionsMonitor</c> consumers so an operator can bump
/// MinAppVersion / LatestKnownAppVersion in appsettings.json without
/// restarting the server (research §17). ServerVersion is resolved
/// from the executing assembly's
/// <see cref="AssemblyInformationalVersionAttribute"/> once at process
/// boot — it cannot change without a redeploy anyway, so caching is
/// safe.
/// </summary>
public sealed class MobileVersionConfig
{
    /// <summary>
    /// The minimum Android app version this server will accept. Apps
    /// older than this are bounced to <c>AppOutdatedScreen</c> by the
    /// native shell (FR-019). Format: flat <c>MAJOR.MINOR.PATCH</c>.
    /// Defaults to <c>"1.0.0"</c> per <c>contracts/me-features.md</c>
    /// assertion #9.
    /// </summary>
    public string MinAppVersion { get; set; } = "1.0.0";

    /// <summary>
    /// The most recent Android app version the vendor has published.
    /// Apps older than this see the dismissable <c>UpdateAvailableBanner</c>
    /// (FR-020). Format: flat <c>MAJOR.MINOR.PATCH</c>. Defaults to
    /// <see cref="ResolveServerVersion"/> per
    /// <c>contracts/me-features.md</c> assertion #10 (sensible default —
    /// the version that ships in lock-step with this server is the
    /// latest).
    /// </summary>
    public string LatestKnownAppVersion { get; set; } = "";

    /// <summary>
    /// Resolve the configured <see cref="LatestKnownAppVersion"/>, or
    /// fall back to <paramref name="serverVersion"/> when blank.
    /// Keeps the appsettings.json optional — operators only need to
    /// set this key when a new mobile build ships AHEAD of the server.
    /// </summary>
    public string EffectiveLatestKnownAppVersion(string serverVersion) =>
        string.IsNullOrWhiteSpace(LatestKnownAppVersion) ? serverVersion : LatestKnownAppVersion;

    /// <summary>
    /// Reads the EgyptTax.Web assembly's
    /// <see cref="AssemblyInformationalVersionAttribute"/> for the
    /// <c>serverVersion</c> field of the
    /// <c>GET /api/v1/me/features</c> response. Cached at first call.
    /// </summary>
    public static string ResolveServerVersion() =>
        _cachedServerVersion ??= ResolveServerVersionUncached();

    private static string? _cachedServerVersion;

    private static string ResolveServerVersionUncached()
    {
        // InformationalVersion typically holds the full SemVer
        // (including pre-release tags); we want the leading
        // MAJOR.MINOR.PATCH per the contract's regex.
        var raw = typeof(MobileVersionConfig).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(MobileVersionConfig).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        // Strip any "+commitsha" or "-prerelease" suffix.
        var plusAt = raw.IndexOf('+');
        if (plusAt > 0) raw = raw[..plusAt];
        var dashAt = raw.IndexOf('-');
        if (dashAt > 0) raw = raw[..dashAt];
        return raw;
    }
}
