using EgyptTax.Application.Audit;
using EgyptTax.Application.Common.Abstractions;
using MediatR;

namespace EgyptTax.Application.Common.Behaviors;

/// <summary>
/// On successful handler completion, calls
/// <see cref="IAuditableRequest.BuildAuditPayload(object?, ICurrentUser)"/>
/// for any request that opts in via <see cref="IAuditableRequest"/> and
/// appends the result to <see cref="IAuditLogStore"/>. Per INV-001 this
/// behaviour MUST be registered INSIDE TransactionBehavior so the audit
/// row is part of the same transaction as the source change. If the
/// handler throws, no audit row is emitted (the caller's transaction
/// rollback would discard it anyway, but skipping the call keeps logs
/// clean).
/// </summary>
public sealed class AuditEmitBehavior<TRequest, TResponse>(IAuditLogStore store, ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next();

        if (request is IAuditableRequest auditable)
        {
            var payload = auditable.BuildAuditPayload(response, _currentUser);
            await _store.AppendAsync(payload, cancellationToken);
        }

        return response;
    }
}
