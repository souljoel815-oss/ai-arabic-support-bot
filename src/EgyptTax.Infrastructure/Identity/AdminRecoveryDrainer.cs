using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Identity;

/// <summary>
/// FR-038 — drains pending <see cref="AdminRecoveryRecord"/> rows
/// written by the out-of-band <c>recover-admin</c> CLI tool and emits
/// one FR-028 audit event per row through the chain-aware
/// <c>SqlAuditLogStore</c>. Called once during application startup
/// (after migrations apply) so the audit chain reflects every recovery
/// even though the CLI cannot append to the chain itself.
/// </summary>
public sealed class AdminRecoveryDrainer(AppDbContext db, IClock clock, IAuditLogStore auditLog)
{
    private readonly AppDbContext _db = db;
    private readonly IClock _clock = clock;
    private readonly IAuditLogStore _auditLog = auditLog;

    public async Task<int> DrainAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _db.Set<AdminRecoveryRecord>()
            .Where(r => r.AuditEmittedAtUtc == null)
            .OrderBy(r => r.RecoveredAtUtc)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        foreach (var record in pending)
        {
            await _auditLog.AppendAsync(
                new AuditLogPayload(
                    Kind: "admin.recovery_drained",
                    ActorUserId: record.TargetUserId,
                    ActorFirmName: null,
                    CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"recovery_id":"{{record.Id:D}}","target_user_id":"{{record.TargetUserId:D}}","target_email":"{{record.TargetEmail}}","recovered_at_utc":"{{record.RecoveredAtUtc:o}}","machine_name":"{{record.MachineName}}","operator_identity":{{(record.OperatorIdentity is null ? "null" : "\"" + record.OperatorIdentity + "\"")}}}"""
                ),
                cancellationToken
            );

            record.MarkAuditEmitted(_clock.UtcNow);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }
}
