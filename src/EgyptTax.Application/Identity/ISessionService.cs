using EgyptTax.Domain.Identity;

namespace EgyptTax.Application.Identity;

/// <summary>
/// FR-039 — port for opening, touching, validating, and revoking
/// per-user authenticated sessions. The Web layer adapts this to the
/// ASP.NET Core cookie pipeline (T050); background jobs and tests
/// drive the port directly. Implementations MUST emit a single
/// <c>session.expired</c> audit event the first time
/// <see cref="ValidateAsync"/> observes a timeout, and a
/// <c>session.revoked</c> event on explicit
/// <see cref="RevokeAsync"/> calls.
/// </summary>
public interface ISessionService
{
    Task<Session> BeginAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<SessionValidationResult> ValidateAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid sessionId,
        SessionRevocationReason reason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a per-request validation call.
/// </summary>
public sealed record SessionValidationResult(
    SessionValidationStatus Status,
    Guid? UserId,
    SessionRevocationReason? Reason);

public enum SessionValidationStatus
{
    Valid,
    NotFound,
    Expired,
    Revoked,
}
