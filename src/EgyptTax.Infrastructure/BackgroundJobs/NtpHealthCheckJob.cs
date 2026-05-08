using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// FR-028 / R-23 — periodic NTP health check. Hangfire cron fires every
/// 6 hours; the job queries the configured NTP source, compares the
/// returned UTC time to the system clock, and emits a
/// <c>system.ntp_skew_detected</c> audit event when the absolute skew
/// exceeds <see cref="DefaultSkewThreshold"/>. A successful, in-spec
/// query stays silent so the audit chain isn't polluted with one
/// routine event per six-hour window. NTP transport / protocol
/// failures emit <c>system.ntp_health_check_failed</c> instead — the
/// fact that we couldn't verify the clock is itself audit-worthy.
/// </summary>
public sealed class NtpHealthCheckJob(
    IClock clock,
    INtpTimeClient ntpClient,
    IAuditLogStore auditLog
)
{
    /// <summary>R-23 — default 5-second skew tolerance.</summary>
    public static readonly TimeSpan DefaultSkewThreshold = TimeSpan.FromSeconds(5);

    private readonly IClock _clock = clock;
    private readonly INtpTimeClient _ntpClient = ntpClient;
    private readonly IAuditLogStore _auditLog = auditLog;
    private readonly TimeSpan _skewThreshold = DefaultSkewThreshold;

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        DateTime ntpUtc;
        try
        {
            ntpUtc = await _ntpClient.QueryUtcNowAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _auditLog.AppendAsync(
                new AuditLogPayload(
                    Kind: "system.ntp_health_check_failed",
                    ActorUserId: null,
                    ActorFirmName: null,
                    CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"error":"{{Escape(ex.GetType().FullName ?? "Exception")}}","message":"{{Escape(ex.Message)}}","queried_at_utc":"{{_clock.UtcNow.ToString("o", CultureInfo.InvariantCulture)}}"}"""
                ),
                cancellationToken
            );
            return;
        }

        var skew = ntpUtc - _clock.UtcNow;
        if (skew.Duration() <= _skewThreshold)
        {
            // In-spec — stay silent.
            return;
        }

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "system.ntp_skew_detected",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"system_utc":"{{_clock.UtcNow.ToString("o", CultureInfo.InvariantCulture)}}","ntp_utc":"{{ntpUtc.ToString("o", CultureInfo.InvariantCulture)}}","skew_seconds":{{skew.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}},"threshold_seconds":{{_skewThreshold.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}}}"""
            ),
            cancellationToken
        );
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
