namespace EgyptTax.Application.Common.Abstractions;

/// <summary>
/// Opt-in marker for requests that require an authenticated user.
/// <c>AuthorizationBehavior</c> rejects any request implementing this
/// interface when <see cref="ICurrentUser.IsAuthenticated"/> is false.
/// Per-permission FR-003 checks land in subsequent stages alongside the
/// individual handlers; this marker is the minimum baseline.
/// </summary>
public interface IAuthorizedRequest
{
}
