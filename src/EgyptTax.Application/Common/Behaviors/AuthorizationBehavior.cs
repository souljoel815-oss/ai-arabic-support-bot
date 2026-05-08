using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.Common.Exceptions;
using MediatR;

namespace EgyptTax.Application.Common.Behaviors;

/// <summary>
/// Baseline authentication gate. Rejects any request implementing
/// <see cref="IAuthorizedRequest"/> when the
/// <see cref="ICurrentUser"/> is anonymous. Per-permission FR-003 checks
/// land per-handler in subsequent stages and FR-004 (no self-approval) is
/// enforced inside the handler that performs the approval. This behaviour
/// is intentionally minimal — it ensures every authorised request has a
/// known caller; richer policy gates compose on top.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is IAuthorizedRequest && !_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException();
        }

        return await next();
    }
}
