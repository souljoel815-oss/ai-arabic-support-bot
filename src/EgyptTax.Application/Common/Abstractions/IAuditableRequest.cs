using EgyptTax.Domain.Audit;

namespace EgyptTax.Application.Common.Abstractions;

/// <summary>
/// Opt-in marker for requests that should emit an audit-log row on
/// successful handler completion. Implementing types build the payload
/// from their own state plus the handler's result, so audit content stays
/// close to the request that produced it (vs. discovered via reflection
/// or convention).
/// </summary>
public interface IAuditableRequest
{
    AuditLogPayload BuildAuditPayload(object? result, ICurrentUser currentUser);
}
