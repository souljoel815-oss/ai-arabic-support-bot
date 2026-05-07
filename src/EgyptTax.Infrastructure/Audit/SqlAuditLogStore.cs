using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Audit;

/// <summary>
/// SQL Server-backed append-only audit log store. Each call to
/// <see cref="AppendAsync"/> opens (or joins) a transaction, takes the
/// previous-tail entry under <c>UPDLOCK,SERIALIZABLE</c> hints to compute
/// <c>prev_hash</c>, allocates the next monotonic <c>Index</c>, computes
/// <c>this_hash</c> via <see cref="AuditChainHasher"/>, and inserts. The
/// allocator-by-tail-row pattern guarantees gap-free indices under
/// concurrency without relying on SQL <c>IDENTITY</c> (which would consume
/// indices on rollback).
/// </summary>
public sealed class SqlAuditLogStore(AppDbContext db) : IAuditLogStore
{
    private readonly AppDbContext _db = db;

    public async Task<AuditLogEntry> AppendAsync(
        AuditLogPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Read the current chain tail under a row-locking hint so concurrent
        // inserts serialize on the highest existing index. The pattern uses
        // an MS SQL Server-specific lock hint; this is the single piece of
        // SqlServer-coupled behaviour in the audit store.
        var tail = await _db.Set<AuditLogEntry>()
            .FromSqlRaw(
                "SELECT TOP (1) * FROM [audit].[audit_log] WITH (UPDLOCK, HOLDLOCK) ORDER BY [index] DESC")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var nextIndex = (tail?.Index ?? 0) + 1;
        var prevHash = tail?.ThisHash ?? AuditChainHasher.GenesisHash;
        var thisHash = AuditChainHasher.ComputeHash(payload.PayloadJson, prevHash);

        var entry = new AuditLogEntry(
            index: nextIndex,
            tsUtc: DateTime.UtcNow,
            actorUserId: payload.ActorUserId,
            actorFirmName: payload.ActorFirmName,
            companyId: payload.CompanyId,
            kind: payload.Kind,
            payloadJson: payload.PayloadJson,
            prevHash: prevHash,
            thisHash: thisHash);

        _db.Set<AuditLogEntry>().Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return entry;
    }
}
