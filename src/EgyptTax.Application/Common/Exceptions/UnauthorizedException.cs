namespace EgyptTax.Application.Common.Exceptions;

/// <summary>
/// Raised by <c>AuthorizationBehavior</c> when a request implements
/// <see cref="EgyptTax.Application.Common.Abstractions.IAuthorizedRequest"/>
/// but no authenticated user is on the
/// <see cref="EgyptTax.Application.Common.Abstractions.ICurrentUser"/>
/// service.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string? message = null)
        : base(message ?? "An authenticated user is required for this request.")
    {
    }
}
