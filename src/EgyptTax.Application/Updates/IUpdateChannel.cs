namespace EgyptTax.Application.Updates;

/// <summary>
/// G4.1 — port for the "is there a newer version?" check. Two
/// implementations shipped:
///   * <c>HttpUpdateChannel</c> — production: GETs the configured
///     <c>UpdateChannelUrl</c> (default
///     <c>https://daftarx.com/latest.json</c>) and returns the
///     deserialised manifest.
///   * <c>NullUpdateChannel</c> — test / offline default; always
///     returns null so the banner never shows.
///
/// The job that calls this is <c>UpdateCheckJob</c>; it runs daily,
/// stores the result on <c>UpdateStatus</c>, and surfaces it via
/// the topbar banner. We do NOT auto-download or auto-install —
/// the operator clicks through to the official download page after
/// reading the changelog.
/// </summary>
public interface IUpdateChannel
{
    Task<UpdateManifest?> CheckLatestAsync(CancellationToken cancellationToken = default);
}

/// <summary>Schema served from
/// <c>https://daftarx.com/latest.json</c>. JSON property names match
/// the field names lower-cased (handled by JsonSerializer
/// PropertyNameCaseInsensitive=true).</summary>
public sealed record UpdateManifest(
    string LatestVersion,
    string MinSupportedVersion,
    string DownloadUrl,
    DateTime ReleasedAt,
    string ChangelogAr,
    string ChangelogEn);
