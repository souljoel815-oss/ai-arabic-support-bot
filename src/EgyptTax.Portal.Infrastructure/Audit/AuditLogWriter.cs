using EgyptTax.Portal.Application.Audit;
using EgyptTax.Portal.Infrastructure.Persistence;
using EgyptTax.Portal.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Portal.Infrastructure.Audit;

/// <summary>
/// T027 per FR-023 + FR-028. Concrete writer for the audit log; the port
/// (<see cref="IAuditLogWriter"/>) lives in
/// <c>EgyptTax.Portal.Application/Audit/</c>. Every customer-visible state
/// change MUST go through this — never insert <c>AuditLogEntry</c> rows
/// directly from a handler.
///
/// The payload object is JSON-serialised; callers are responsible for
/// stripping tokens, password hashes, and PII before passing it in. The
/// writer rejects the row if it detects obvious secret-like substrings as a
/// defence-in-depth check, not a substitute for caller discipline.
/// </summary>
internal sealed class AuditLogWriter : IAuditLogWriter
{
    private static readonly string[] ForbiddenSubstrings =
    {
        "password", "secret", "token", "api_key", "apikey", "hwid_token",
        "private_key", "privatekey", "signed_token",
    };

    private readonly PortalDbContext _db;
    private readonly ILogger<AuditLogWriter> _logger;

    public AuditLogWriter(PortalDbContext db, ILogger<AuditLogWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task WriteAsync(AuditLogEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (@event.OrganisationId == Guid.Empty)
        {
            throw new ArgumentException("OrganisationId must be set on every audit-log row.", nameof(@event));
        }
        if (string.IsNullOrWhiteSpace(@event.Verb))
        {
            throw new ArgumentException("Verb must be set on every audit-log row.", nameof(@event));
        }
        if (string.IsNullOrWhiteSpace(@event.SubjectKind))
        {
            throw new ArgumentException("SubjectKind must be set on every audit-log row.", nameof(@event));
        }

        string? payloadJson = null;
        if (@event.Payload is not null)
        {
            payloadJson = System.Text.Json.JsonSerializer.Serialize(@event.Payload);
            foreach (var forbidden in ForbiddenSubstrings)
            {
                if (payloadJson.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(
                        "Refusing to write audit-log row for verb {Verb}: payload contains forbidden substring '{Forbidden}'. Caller must redact before passing.",
                        @event.Verb,
                        forbidden);
                    throw new InvalidOperationException(
                        $"Refusing to write audit-log row for verb '{@event.Verb}': payload contains forbidden substring '{forbidden}'. Redact at the call site.");
                }
            }
        }

        var row = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OrganisationId = @event.OrganisationId,
            ActorTeamMemberId = @event.ActorTeamMemberId,
            ActorDisplayNameSnapshot = @event.ActorDisplayNameSnapshot,
            Verb = @event.Verb,
            SubjectKind = @event.SubjectKind,
            SubjectId = @event.SubjectId,
            PayloadJson = payloadJson,
            OriginatingIp = @event.OriginatingIp,
            OccurredAtUtc = DateTime.UtcNow,
        };

        _db.AuditLogEntries.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Audit: {Verb} on {SubjectKind} {SubjectId} by {Actor} in org {Org}",
            @event.Verb,
            @event.SubjectKind,
            @event.SubjectId,
            @event.ActorDisplayNameSnapshot,
            @event.OrganisationId);
    }
}
