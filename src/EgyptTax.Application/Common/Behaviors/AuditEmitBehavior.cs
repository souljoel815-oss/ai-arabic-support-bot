using EgyptTax.Application.Audit;
using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.FirmPortal;
using EgyptTax.Domain.Audit;
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
///
/// T225 / FR-049 / US8 — overlays the firm name onto the payload when
/// the actor is a known firm user but the request builder didn't
/// populate <see cref="AuditLogPayload.ActorFirmName"/> itself. Defense
/// in depth so future commands authored by a firm user are
/// automatically firm-tagged in the audit chain (INV-015) without
/// every command having to remember to fill the field. The overlay
/// only fires when ActorFirmName is currently null AND ActorUserId
/// is set; in-house users (no AccountantFirmUser row) come back null
/// from the resolver and the payload passes through unchanged.
/// </summary>
public sealed class AuditEmitBehavior<TRequest, TResponse>(
    IAuditLogStore store,
    ICurrentUser currentUser,
    IFirmContextResolver firmContextResolver
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IFirmContextResolver _firmContextResolver = firmContextResolver;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next();

        if (request is IAuditableRequest auditable)
        {
            var payload = auditable.BuildAuditPayload(response, _currentUser);

            if (
                payload.ActorFirmName is null
                && payload.ActorUserId is { } actorUserId
                && actorUserId != Guid.Empty
            )
            {
                var firmName = await _firmContextResolver.ResolveFirmNameAsync(
                    actorUserId,
                    cancellationToken
                );
                if (firmName is not null)
                {
                    payload = payload with { ActorFirmName = firmName };
                }
            }

            await _store.AppendAsync(payload, cancellationToken);
        }

        return response;
    }
}
