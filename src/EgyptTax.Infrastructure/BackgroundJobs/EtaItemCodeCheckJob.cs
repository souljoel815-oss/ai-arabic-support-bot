using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// P1.6 — Hangfire job that polls the GS1 / EGS registries for
/// items whose code request is still Pending, and either:
///
///   * Promotes the item to <c>EtaItemCodeStatus.Active</c> with
///     the issued code, OR
///   * Flips it to <c>EtaItemCodeStatus.Failed</c> with the
///     registry's verbatim rejection reason.
///
/// One FR-028 audit event per state transition so the chain
/// records when each SKU became citable on invoice lines.
/// </summary>
public sealed class EtaItemCodeCheckJob
{
    private readonly AppDbContext _db;
    private readonly IEtaItemCodeRegistry _registry;
    private readonly IAuditLogStore _auditLog;
    private readonly IClock _clock;

    public EtaItemCodeCheckJob(
        AppDbContext db,
        IEtaItemCodeRegistry registry,
        IAuditLogStore auditLog,
        IClock clock)
    {
        _db = db;
        _registry = registry;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<EtaItemCodeCheckResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _db.Set<Item>()
            .Where(i =>
                (i.EtaCodeStatus == EtaItemCodeStatus.PendingGs1
                || i.EtaCodeStatus == EtaItemCodeStatus.PendingEgs)
                && i.EtaCodeRequestedAtUtc != null)
            .OrderBy(i => i.EtaCodeRequestedAtUtc)
            .ToListAsync(cancellationToken);

        var activated = 0;
        var failed = 0;
        var stillPending = 0;

        foreach (var item in pending)
        {
            if (item.EtaCodeKind is null || item.EtaCodeRequestedAtUtc is null) continue;

            EtaItemCodeLookupResult result;
            try
            {
                result = await _registry.LookupAsync(
                    item.Id, item.EtaCodeKind.Value, item.EtaCodeRequestedAtUtc.Value, cancellationToken);
            }
            catch
            {
                continue; // skip this row, retry next tick
            }

            switch (result.Status)
            {
                case EtaItemCodeLookupStatus.Active:
                    if (string.IsNullOrWhiteSpace(result.IssuedCode)) continue;
                    item.MarkEtaCodeActive(result.IssuedCode, _clock.UtcNow);
                    await _db.SaveChangesAsync(cancellationToken);
                    await EmitAsync("item.eta_code_activated", item, result.IssuedCode!, cancellationToken);
                    activated++;
                    break;

                case EtaItemCodeLookupStatus.Failed:
                    item.MarkEtaCodeFailed(result.FailureReason ?? "Unknown failure", _clock.UtcNow);
                    await _db.SaveChangesAsync(cancellationToken);
                    await EmitAsync("item.eta_code_failed", item, result.FailureReason ?? "", cancellationToken);
                    failed++;
                    break;

                case EtaItemCodeLookupStatus.StillPending:
                default:
                    stillPending++;
                    break;
            }
        }

        return new EtaItemCodeCheckResult(
            CandidateCount: pending.Count,
            ActivatedCount: activated,
            FailedCount: failed,
            StillPendingCount: stillPending);
    }

    private async Task EmitAsync(string kind, Item item, string detail, CancellationToken cancellationToken)
    {
        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: kind,
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"item_id":"{{item.Id:D}}","item_code":"{{item.Code}}","registry":"{{item.EtaCodeKind}}","detail":"{{detail}}"}"""
            ),
            cancellationToken);
    }
}

public sealed record EtaItemCodeCheckResult(
    int CandidateCount,
    int ActivatedCount,
    int FailedCount,
    int StillPendingCount);
