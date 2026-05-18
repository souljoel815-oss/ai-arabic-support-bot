namespace EgyptTax.Portal.Application.Audit;

/// <summary>
/// T027 per FR-023 + FR-028. Port for the audit-log writer. Implementation
/// in <c>Infrastructure/Audit/AuditLogWriter.cs</c>.
/// </summary>
public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogEvent @event, CancellationToken cancellationToken = default);
}

public sealed record AuditLogEvent(
    Guid OrganisationId,
    Guid? ActorTeamMemberId,
    string ActorDisplayNameSnapshot,
    string Verb,
    string SubjectKind,
    string SubjectId,
    object? Payload,
    string OriginatingIp);
