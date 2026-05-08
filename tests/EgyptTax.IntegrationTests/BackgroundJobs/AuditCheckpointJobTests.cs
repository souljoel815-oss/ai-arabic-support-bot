using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.BackgroundJobs;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.IntegrationTests.BackgroundJobs;

/// <summary>
/// T056 — FR-028 audit integrity checkpoint emitter. The job MUST emit
/// a fresh checkpoint when EITHER condition fires (whichever first):
///   (a) 1,000 new entries since the previous checkpoint, OR
///   (b) 15 minutes elapsed since the previous checkpoint.
/// The checkpoint records (last_index, last_hash, ts_utc) of the
/// current chain tail so the FR-028 verifier can detect tail
/// truncation against the latest checkpoint.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AuditCheckpointJobTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task RunOnce_NoEntries_DoesNotEmitCheckpoint()
    {
        await using var db = await _fixture.CreateContextAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 9, 0, 0, DateTimeKind.Utc));
        var checkpoints = new SqlSchemaCheckpointStore(db);
        var job = new AuditCheckpointJob(db, checkpoints, clock);

        var result = await job.RunOnceAsync(CancellationToken.None);

        result
            .EmittedNewCheckpoint.Should()
            .BeFalse(because: "no audit entries exist yet; the job MUST be a no-op");
        result.LastIndex.Should().BeNull();

        var written = await checkpoints.ReadLatestAsync();
        written.Should().BeNull();
    }

    [Fact]
    public async Task RunOnce_FirstEntries_AlwaysEmitsCheckpoint()
    {
        await using var db = await _fixture.CreateContextAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 9, 0, 0, DateTimeKind.Utc));
        var checkpoints = new SqlSchemaCheckpointStore(db);
        var auditStore = new SqlAuditLogStore(db);
        var job = new AuditCheckpointJob(db, checkpoints, clock);

        for (var i = 0; i < 5; i++)
        {
            await auditStore.AppendAsync(
                new AuditLogPayload(
                    Kind: "test.event",
                    ActorUserId: null,
                    ActorFirmName: null,
                    CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"i":{{i}}}"""
                )
            );
        }

        var result = await job.RunOnceAsync(CancellationToken.None);

        result
            .EmittedNewCheckpoint.Should()
            .BeTrue(
                because: "the first checkpoint MUST be written even though the 1k threshold is unmet"
            );
        result.LastIndex.Should().Be(5);

        var written = await checkpoints.ReadLatestAsync();
        written.Should().NotBeNull();
        written!.LastIndex.Should().Be(5);
    }

    [Fact]
    public async Task RunOnce_FewerThanThresholdNewEntries_AndUnderTimeWindow_DoesNotEmit()
    {
        await using var db = await _fixture.CreateContextAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 9, 0, 0, DateTimeKind.Utc));
        var checkpoints = new SqlSchemaCheckpointStore(db);
        var auditStore = new SqlAuditLogStore(db);
        var job = new AuditCheckpointJob(db, checkpoints, clock);

        // Emit a baseline checkpoint after 10 entries.
        for (var i = 0; i < 10; i++)
        {
            await auditStore.AppendAsync(
                new AuditLogPayload("test.event", null, null, Guid.Empty, "{}")
            );
        }
        var first = await job.RunOnceAsync(CancellationToken.None);
        first.EmittedNewCheckpoint.Should().BeTrue();

        // Add 100 more entries (well below the 1k threshold) and advance
        // the clock by 5 minutes (well below the 15-min threshold).
        for (var i = 0; i < 100; i++)
        {
            await auditStore.AppendAsync(
                new AuditLogPayload("test.event", null, null, Guid.Empty, "{}")
            );
        }
        clock.Advance(TimeSpan.FromMinutes(5));

        var second = await job.RunOnceAsync(CancellationToken.None);
        second
            .EmittedNewCheckpoint.Should()
            .BeFalse(because: "neither the 1k-entry nor the 15-min trigger has fired");

        var written = await checkpoints.ReadLatestAsync();
        written!.LastIndex.Should().Be(10, because: "the prior checkpoint should remain unchanged");
    }

    [Fact]
    public async Task RunOnce_FifteenMinuteWindow_EmitsCheckpoint()
    {
        await using var db = await _fixture.CreateContextAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 9, 0, 0, DateTimeKind.Utc));
        var checkpoints = new SqlSchemaCheckpointStore(db);
        var auditStore = new SqlAuditLogStore(db);
        var job = new AuditCheckpointJob(db, checkpoints, clock);

        for (var i = 0; i < 10; i++)
        {
            await auditStore.AppendAsync(
                new AuditLogPayload("test.event", null, null, Guid.Empty, "{}")
            );
        }
        await job.RunOnceAsync(CancellationToken.None);

        // Add a single entry then jump past the 15-min window.
        await auditStore.AppendAsync(
            new AuditLogPayload("test.event", null, null, Guid.Empty, "{}")
        );
        clock.Advance(TimeSpan.FromMinutes(16));

        var second = await job.RunOnceAsync(CancellationToken.None);
        second
            .EmittedNewCheckpoint.Should()
            .BeTrue(
                because: "the 15-min time window MUST trigger a fresh checkpoint even with only 1 new entry"
            );
        second.LastIndex.Should().Be(11);
    }

    [Fact]
    public async Task RunOnce_OneThousandNewEntries_EmitsCheckpoint()
    {
        await using var db = await _fixture.CreateContextAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 9, 0, 0, DateTimeKind.Utc));
        var checkpoints = new SqlSchemaCheckpointStore(db);
        var auditStore = new SqlAuditLogStore(db);
        var job = new AuditCheckpointJob(db, checkpoints, clock);

        // Establish a baseline checkpoint at index 5.
        for (var i = 0; i < 5; i++)
        {
            await auditStore.AppendAsync(
                new AuditLogPayload("test.event", null, null, Guid.Empty, "{}")
            );
        }
        await job.RunOnceAsync(CancellationToken.None);

        // Append exactly 1,000 more entries, all within the 15-min window.
        for (var i = 0; i < 1_000; i++)
        {
            await auditStore.AppendAsync(
                new AuditLogPayload("test.event", null, null, Guid.Empty, "{}")
            );
        }
        clock.Advance(TimeSpan.FromMinutes(2));

        var second = await job.RunOnceAsync(CancellationToken.None);
        second
            .EmittedNewCheckpoint.Should()
            .BeTrue(
                because: "the 1k-entry trigger MUST fire even when the time window has not lapsed"
            );
        second.LastIndex.Should().Be(1_005);
    }

    private sealed class TestClock(DateTime initial) : IClock
    {
        public DateTime UtcNow { get; private set; } = initial;

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }
}
