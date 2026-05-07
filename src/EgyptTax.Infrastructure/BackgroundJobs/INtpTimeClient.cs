namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// Port over an SNTP/NTP client query. Production wires
/// <see cref="SntpTimeClient"/> against <c>pool.ntp.org</c>; tests
/// inject a stub that returns a deterministic value (or throws to
/// simulate transport failure).
/// </summary>
public interface INtpTimeClient
{
    /// <summary>
    /// Returns the authoritative UTC time reported by the configured
    /// NTP source. May throw on transport / protocol failure; the
    /// <see cref="NtpHealthCheckJob"/> catches the throw and records a
    /// dedicated <c>system.ntp_health_check_failed</c> audit event.
    /// </summary>
    Task<DateTime> QueryUtcNowAsync(CancellationToken cancellationToken = default);
}
