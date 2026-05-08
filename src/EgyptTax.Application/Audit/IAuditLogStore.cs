using EgyptTax.Domain.Audit;

namespace EgyptTax.Application.Audit;

/// <summary>
/// Application-layer port for the FR-028 audit log. The Infrastructure
/// implementation (<c>SqlAuditLogStore</c>) writes a SHA-256-chained row
/// per call; <c>AuditEmitBehavior</c> consumes this port from inside the
/// transaction so the row commits with the source change.
/// </summary>
public interface IAuditLogStore
{
    Task<AuditLogEntry> AppendAsync(
        AuditLogPayload payload,
        CancellationToken cancellationToken = default
    );
}
