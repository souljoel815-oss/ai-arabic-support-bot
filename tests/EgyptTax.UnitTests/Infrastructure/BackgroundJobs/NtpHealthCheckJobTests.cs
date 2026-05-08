using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.BackgroundJobs;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.UnitTests.Infrastructure.BackgroundJobs;

/// <summary>
/// T055 — FR-028 / R-23 NTP health-check job. The job MUST query an
/// authoritative NTP source, compare the returned UTC time to the
/// system clock, and emit a <c>system.ntp_skew_detected</c> audit event
/// when the absolute skew exceeds the configured threshold (default
/// 5 seconds). The job logs success silently otherwise so the audit
/// chain isn't polluted with one routine event every 6 hours.
/// </summary>
public class NtpHealthCheckJobTests
{
    private static readonly DateTime Anchor = new(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunOnceAsync_WhenSkewIsZero_DoesNotEmitAuditEvent()
    {
        var clock = new FixedClock(Anchor);
        var ntp = new StubNtpClient(Anchor);
        var captured = new CaptureStore();
        var job = new NtpHealthCheckJob(clock, ntp, captured);

        await job.RunOnceAsync(CancellationToken.None);

        captured
            .Captured.Should()
            .BeEmpty(because: "no skew was detected; the audit chain MUST stay quiet");
    }

    [Fact]
    public async Task RunOnceAsync_WhenSkewBelowThreshold_DoesNotEmitAuditEvent()
    {
        var clock = new FixedClock(Anchor);
        var ntp = new StubNtpClient(Anchor.AddSeconds(2));
        var captured = new CaptureStore();
        var job = new NtpHealthCheckJob(clock, ntp, captured);

        await job.RunOnceAsync(CancellationToken.None);

        captured
            .Captured.Should()
            .BeEmpty(because: "2-second skew is within the default 5-second tolerance");
    }

    [Fact]
    public async Task RunOnceAsync_WhenSkewAboveThreshold_EmitsAuditEvent()
    {
        var clock = new FixedClock(Anchor);
        var ntp = new StubNtpClient(Anchor.AddSeconds(15));
        var captured = new CaptureStore();
        var job = new NtpHealthCheckJob(clock, ntp, captured);

        await job.RunOnceAsync(CancellationToken.None);

        captured
            .Captured.Should()
            .ContainSingle(
                e => e.Kind == "system.ntp_skew_detected",
                because: "15-second skew exceeds the 5-second tolerance"
            );
    }

    [Fact]
    public async Task RunOnceAsync_WhenNtpQueryFails_EmitsHealthFailureAuditEvent()
    {
        var clock = new FixedClock(Anchor);
        var ntp = new ThrowingNtpClient();
        var captured = new CaptureStore();
        var job = new NtpHealthCheckJob(clock, ntp, captured);

        await job.RunOnceAsync(CancellationToken.None);

        captured
            .Captured.Should()
            .ContainSingle(
                e => e.Kind == "system.ntp_health_check_failed",
                because: "an NTP query failure is itself a fact worth recording"
            );
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class StubNtpClient(DateTime ntpNow) : INtpTimeClient
    {
        public Task<DateTime> QueryUtcNowAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ntpNow);
    }

    private sealed class ThrowingNtpClient : INtpTimeClient
    {
        public Task<DateTime> QueryUtcNowAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("NTP server unreachable.");
    }

    private sealed class CaptureStore : IAuditLogStore
    {
        public List<AuditLogPayload> Captured { get; } = [];

        public Task<AuditLogEntry> AppendAsync(
            AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            Captured.Add(payload);
            var entry = new AuditLogEntry(
                index: Captured.Count,
                tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId,
                actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId,
                kind: payload.Kind,
                payloadJson: payload.PayloadJson,
                prevHash: new byte[32],
                thisHash: new byte[32]
            );
            return Task.FromResult(entry);
        }
    }
}
