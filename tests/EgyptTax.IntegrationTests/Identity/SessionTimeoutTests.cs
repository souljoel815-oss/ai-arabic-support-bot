using EgyptTax.Application.Audit;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Identity;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;

namespace EgyptTax.IntegrationTests.Identity;

/// <summary>
/// T053 — FR-039 session policy: idle &gt; 30 min OR absolute &gt; 12 h
/// expires the session. <see cref="SessionService.ValidateAsync"/> MUST
/// return Expired and emit a single `session.expired` audit event the
/// first time it observes an expired session, and SHOULD then keep
/// returning Expired without emitting duplicate events.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SessionTimeoutTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task InactivityTimeout_ExpiresSession_AndEmitsLogoutAuditEvent()
    {
        await using var db = await _fixture.CreateContextAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 9, 0, 0, DateTimeKind.Utc));
        var auditCapture = new CaptureAuditLogStore();
        var sessions = new SessionService(db, clock, auditCapture);
        var userId = await SeedUserAsync(db);

        var session = await sessions.BeginAsync(
            userId,
            "127.0.0.1",
            "xunit",
            CancellationToken.None
        );

        // 10 min later — still valid.
        clock.Advance(TimeSpan.FromMinutes(10));
        var midSession = await sessions.ValidateAsync(session.Id, CancellationToken.None);
        midSession.Status.Should().Be(SessionValidationStatus.Valid);

        // Touch resets the inactivity counter.
        await sessions.TouchAsync(session.Id, CancellationToken.None);

        // Now jump 31 min past last touch — inactivity threshold breached.
        clock.Advance(TimeSpan.FromMinutes(31));
        var expired = await sessions.ValidateAsync(session.Id, CancellationToken.None);
        expired.Status.Should().Be(SessionValidationStatus.Expired);
        expired.Reason.Should().Be(SessionRevocationReason.InactivityTimeout);

        auditCapture
            .Captured.Should()
            .ContainSingle(
                e => e.Kind == "session.expired",
                because: "FR-028 + FR-039 — exactly one audit event MUST be emitted on the first observation of expiry"
            );

        // Subsequent validations stay Expired but do not duplicate the event.
        var second = await sessions.ValidateAsync(session.Id, CancellationToken.None);
        second.Status.Should().Be(SessionValidationStatus.Expired);
        auditCapture
            .Captured.Count(e => e.Kind == "session.expired")
            .Should()
            .Be(1, because: "duplicate logout events would pollute the audit trail");
    }

    [Fact]
    public async Task AbsoluteTimeout_ExpiresSession_EvenWhenContinuouslyTouched()
    {
        await using var db = await _fixture.CreateContextAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 0, 0, 0, DateTimeKind.Utc));
        var auditCapture = new CaptureAuditLogStore();
        var sessions = new SessionService(db, clock, auditCapture);
        var userId = await SeedUserAsync(db);

        var session = await sessions.BeginAsync(
            userId,
            "127.0.0.1",
            "xunit",
            CancellationToken.None
        );

        // Simulate a busy user — touch every 5 min for 12+ hours. The absolute
        // expiry SHOULD still fire because it is anchored at issuance, not last
        // activity.
        for (var i = 0; i < 145; i++) // 145 * 5min = 12h05m
        {
            clock.Advance(TimeSpan.FromMinutes(5));
            await sessions.TouchAsync(session.Id, CancellationToken.None);
        }

        var validation = await sessions.ValidateAsync(session.Id, CancellationToken.None);
        validation.Status.Should().Be(SessionValidationStatus.Expired);
        validation.Reason.Should().Be(SessionRevocationReason.AbsoluteTimeout);

        auditCapture
            .Captured.Should()
            .ContainSingle(
                e => e.Kind == "session.expired",
                because: "absolute-timeout expiry MUST also emit the FR-039 session.expired event"
            );
    }

    private static async Task<Guid> SeedUserAsync(
        EgyptTax.Infrastructure.Persistence.AppDbContext db
    )
    {
        var user = new User(
            email: $"u-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مستخدم", "User"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed class TestClock(DateTime initial) : EgyptTax.SharedKernel.Time.IClock
    {
        public DateTime UtcNow { get; private set; } = initial;

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public List<AuditLogPayload> Captured { get; } = [];

        public Task<AuditLogEntry> AppendAsync(
            AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            Captured.Add(payload);
            var entry = new AuditLogEntry(
                index: Captured.Count,
                tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId,
                actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId,
                kind: payload.Kind,
                payloadJson: payload.PayloadJson,
                prevHash: new byte[32],
                thisHash: new byte[32]
            );
            return Task.FromResult(entry);
        }
    }
}
