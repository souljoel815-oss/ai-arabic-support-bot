using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// FR-028 — periodic audit-chain checkpoint emitter. Runs frequently
/// (Hangfire cron, default every minute) but only writes a fresh
/// checkpoint when EITHER trigger fires (whichever comes first):
///   (a) at least <see cref="EntryCountThreshold"/> new entries since
///       the last checkpoint, OR
///   (b) at least <see cref="TimeWindow"/> elapsed since the last
///       checkpoint.
/// The first checkpoint (no prior checkpoint exists) is always
/// written when there is at least one audit entry, so the verifier
/// has something concrete to compare the chain head against.
/// </summary>
public sealed class AuditCheckpointJob(
    AppDbContext db,
    IAuditCheckpointStore checkpoints,
    IClock clock
)
{
    /// <summary>FR-028 — write at every 1,000 entries.</summary>
    public const int EntryCountThreshold = 1_000;

    /// <summary>FR-028 — or every 15 minutes, whichever fires first.</summary>
    public static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db = db;
    private readonly IAuditCheckpointStore _checkpoints = checkpoints;
    private readonly IClock _clock = clock;

    public async Task<AuditCheckpointJobResult> RunOnceAsync(
        CancellationToken cancellationToken = default
    )
    {
        var tail = await _db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderByDescending(e => e.Index)
            .Select(e => new TailRow(e.Index, e.ThisHash))
            .FirstOrDefaultAsync(cancellationToken);

        if (tail is null)
        {
            // No audit entries at all — nothing to checkpoint.
            return new AuditCheckpointJobResult(false, null);
        }

        var existing = await _checkpoints.ReadLatestAsync(cancellationToken);
        var nowUtc = _clock.UtcNow;

        var shouldEmit =
            existing is null
            || tail.Index - existing.LastIndex >= EntryCountThreshold
            || nowUtc - existing.TsUtc >= TimeWindow;

        if (!shouldEmit)
        {
            return new AuditCheckpointJobResult(false, tail.Index);
        }

        var checkpoint = new AuditCheckpoint(tail.Index, tail.Hash, nowUtc);
        await _checkpoints.WriteAsync(checkpoint, cancellationToken);

        return new AuditCheckpointJobResult(true, tail.Index);
    }

    private sealed record TailRow(long Index, byte[] Hash);
}

/// <summary>
/// Outcome of one <see cref="AuditCheckpointJob.RunOnceAsync"/> tick.
/// Surfaced from the job so the integration tests can assert which
/// branch fired.
/// </summary>
public sealed record AuditCheckpointJobResult(bool EmittedNewCheckpoint, long? LastIndex);
