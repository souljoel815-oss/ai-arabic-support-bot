using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Identity;

/// <summary>
/// FR-039 — server-side session lifecycle service. Owns the
/// inactivity (30 min) and absolute (12 h) policy windows; emits
/// <c>session.expired</c> on the first observation of timeout and
/// <c>session.revoked</c> on explicit user / administrator revocation.
/// The cookie pipeline (T050) drives this service via the
/// <c>OnValidatePrincipal</c> hook so revocation takes effect on the
/// next protected request.
/// </summary>
public sealed class SessionService(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    : ISessionService
{
    /// <summary>FR-039 default inactivity window.</summary>
    public static readonly TimeSpan DefaultInactivityWindow = TimeSpan.FromMinutes(30);

    /// <summary>FR-039 default absolute lifetime.</summary>
    public static readonly TimeSpan DefaultAbsoluteLifetime = TimeSpan.FromHours(12);

    private readonly AppDbContext _db = db;
    private readonly IClock _clock = clock;
    private readonly IAuditLogStore _auditLog = auditLog;
    private readonly TimeSpan _inactivityWindow = DefaultInactivityWindow;
    private readonly TimeSpan _absoluteLifetime = DefaultAbsoluteLifetime;

    public async Task<Session> BeginAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default
    )
    {
        var nowUtc = _clock.UtcNow;
        var session = new Session(userId, nowUtc, _absoluteLifetime, ipAddress, userAgent);

        _db.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "session.opened",
                ActorUserId: userId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: PayloadJsonForSession(session)
            ),
            cancellationToken
        );

        return session;
    }

    public async Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.Set<Session>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null || session.IsRevoked)
        {
            return;
        }
        session.Touch(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SessionValidationResult> ValidateAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    )
    {
        var session = await _db.Set<Session>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return new SessionValidationResult(SessionValidationStatus.NotFound, null, null);
        }

        if (session.IsRevoked)
        {
            // De-dupe — if a previous validation already revoked it for
            // timeout, the audit event has already been emitted.
            var status = session.RevocationReason
                is SessionRevocationReason.InactivityTimeout
                    or SessionRevocationReason.AbsoluteTimeout
                ? SessionValidationStatus.Expired
                : SessionValidationStatus.Revoked;
            return new SessionValidationResult(status, session.UserId, session.RevocationReason);
        }

        var nowUtc = _clock.UtcNow;
        SessionRevocationReason? expiryReason = null;

        if (nowUtc >= session.AbsoluteExpiresAtUtc)
        {
            expiryReason = SessionRevocationReason.AbsoluteTimeout;
        }
        else if (nowUtc - session.LastActivityAtUtc >= _inactivityWindow)
        {
            expiryReason = SessionRevocationReason.InactivityTimeout;
        }

        if (expiryReason is null)
        {
            return new SessionValidationResult(SessionValidationStatus.Valid, session.UserId, null);
        }

        // First observation of expiry: revoke + emit audit event.
        session.Revoke(nowUtc, expiryReason.Value);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "session.expired",
                ActorUserId: session.UserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: PayloadJsonForExpiry(session, expiryReason.Value)
            ),
            cancellationToken
        );

        return new SessionValidationResult(
            SessionValidationStatus.Expired,
            session.UserId,
            expiryReason
        );
    }

    public async Task RevokeAsync(
        Guid sessionId,
        SessionRevocationReason reason,
        CancellationToken cancellationToken = default
    )
    {
        var session = await _db.Set<Session>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null || session.IsRevoked)
        {
            return;
        }

        session.Revoke(_clock.UtcNow, reason);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "session.revoked",
                ActorUserId: session.UserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: PayloadJsonForRevocation(session, reason)
            ),
            cancellationToken
        );
    }

    private static string PayloadJsonForSession(Session s) =>
        $$"""{"session_id":"{{s.Id:D}}","user_id":"{{s.UserId:D}}","ip":{{JsonStringOrNull(s.IpAddress)}},"user_agent":{{JsonStringOrNull(s.UserAgent)}},"issued_at_utc":"{{s.IssuedAtUtc.ToString("o", CultureInfo.InvariantCulture)}}","absolute_expires_at_utc":"{{s.AbsoluteExpiresAtUtc.ToString("o", CultureInfo.InvariantCulture)}}"}""";

    private static string PayloadJsonForExpiry(Session s, SessionRevocationReason reason) =>
        $$"""{"session_id":"{{s.Id:D}}","user_id":"{{s.UserId:D}}","reason":"{{reason}}","last_activity_at_utc":"{{s.LastActivityAtUtc.ToString("o", CultureInfo.InvariantCulture)}}","absolute_expires_at_utc":"{{s.AbsoluteExpiresAtUtc.ToString("o", CultureInfo.InvariantCulture)}}"}""";

    private static string PayloadJsonForRevocation(Session s, SessionRevocationReason reason) =>
        $$"""{"session_id":"{{s.Id:D}}","user_id":"{{s.UserId:D}}","reason":"{{reason}}"}""";

    private static string JsonStringOrNull(string? value) =>
        value is null
            ? "null"
            : "\""
                + value
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal)
                + "\"";
}
