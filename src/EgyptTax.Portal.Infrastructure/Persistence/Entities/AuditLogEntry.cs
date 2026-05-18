namespace EgyptTax.Portal.Infrastructure.Persistence.Entities;

/// <summary>
/// T024 per data-model.md §8. Immutable record of every customer-visible
/// state change (FR-023). One row per verb. Scoped to OrganisationId so
/// Owners only see their own org's history. Payload is a free-form JSON
/// blob — MUST NOT contain tokens, password hashes, or PII beyond what is
/// already visible in the portal UI. Retained indefinitely (data-model.md
/// §8 retention rule) — never purged by the soft-delete job (FR-024).
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganisationId { get; set; }

    /// <summary>FK to TeamMember (Identity user). Null = system actor (e.g. webhook handler).</summary>
    public Guid? ActorTeamMemberId { get; set; }

    /// <summary>Display name snapshot — so audit log stays readable if a member is removed later.</summary>
    public string ActorDisplayNameSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// Verb in dotted-namespace form: <c>licence.activated</c>,
    /// <c>licence.transferred</c>, <c>licence.retired</c>,
    /// <c>subscription.upgraded</c>, <c>subscription.cancelled</c>,
    /// <c>member.invited</c>, <c>member.removed</c>,
    /// <c>ticket.opened</c>, <c>payment.cleared</c>, etc.
    /// </summary>
    public string Verb { get; set; } = string.Empty;

    /// <summary>What kind of thing the verb acted on: <c>Subscription</c>, <c>Licence</c>, <c>Invitation</c>, etc.</summary>
    public string SubjectKind { get; set; } = string.Empty;

    /// <summary>The subject's identifier (usually a Guid string). NEVER a token value.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>JSON blob with verb-specific context. NEVER tokens, PII, or password hashes.</summary>
    public string? PayloadJson { get; set; }

    public string OriginatingIp { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
