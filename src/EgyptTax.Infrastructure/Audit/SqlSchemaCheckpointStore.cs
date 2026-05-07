using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Audit;

/// <summary>
/// FR-028 checkpoint storage backed by a single-row table in the
/// <c>audit_meta</c> schema. Per the contract, write privileges on the
/// schema are restricted to the application's service account at install
/// time (operator runbook responsibility, not enforced by EF).
/// </summary>
public sealed class SqlSchemaCheckpointStore(AppDbContext db) : IAuditCheckpointStore
{
    private readonly AppDbContext _db = db;

    public async Task<AuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<AuditCheckpointRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);

        return row is null
            ? null
            : new AuditCheckpoint(row.LastIndex, row.LastHash, row.TsUtc);
    }

    public async Task WriteAsync(AuditCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var existing = await _db.Set<AuditCheckpointRow>()
            .SingleOrDefaultAsync(c => c.Id == 1, cancellationToken);

        if (existing is null)
        {
            _db.Set<AuditCheckpointRow>().Add(new AuditCheckpointRow
            {
                Id = 1,
                LastIndex = checkpoint.LastIndex,
                LastHash = checkpoint.LastHash,
                TsUtc = checkpoint.TsUtc,
            });
        }
        else
        {
            existing.LastIndex = checkpoint.LastIndex;
            existing.LastHash = checkpoint.LastHash;
            existing.TsUtc = checkpoint.TsUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
