namespace EgyptTax.Application.Common.Abstractions;

/// <summary>
/// Per-request identity. Web layer adapts an HttpContext-backed implementation;
/// background jobs supply a Hangfire-context implementation; tests inject a
/// fake. Returns nullable identifiers so unauthenticated paths (login,
/// password reset) can resolve the same service without throwing.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? CompanyId { get; }
    string? FirmName { get; }
    bool IsAuthenticated { get; }
}
