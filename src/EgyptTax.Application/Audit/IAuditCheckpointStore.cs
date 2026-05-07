using EgyptTax.Domain.Audit;

namespace EgyptTax.Application.Audit;

/// <summary>
/// Port for the FR-028 audit integrity-checkpoint store. Two implementations
/// are provided in Infrastructure (file-mode and SQL-schema-mode); the
/// operator picks one at install time. Read returns the most recent
/// committed checkpoint or <c>null</c> if the store has never been
/// written to.
/// </summary>
public interface IAuditCheckpointStore
{
    Task<AuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(AuditCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
