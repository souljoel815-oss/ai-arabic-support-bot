using EgyptTax.Application.FirmPortal;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.FirmPortal;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.FirmPortal;

/// <summary>
/// T216 / US8 scenario 1 / FR-049 — invite an external accountant
/// via email; accept; the resulting AccountantFirmUser row tags the
/// user with the firm name; audit-log entries written by that user
/// carry the firm name. Pins the invitation/acceptance handshake +
/// the audit-tagging contract that downstream firm-user actions
/// rely on.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class InvitationAcceptTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Invite_ThenAccept_TagsAuditLogWithFirmName()
    {
        await using var db = await _fixture.CreateContextAsync();

        // Seed an Administrator who issues the invitation.
        var admin = new User(
            email: $"admin-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مسؤول", "Administrator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(admin);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));
        var auditStore = new SqlAuditLogStore(db);
        var handler = new InviteAccountantFirmUserHandler(db, clock, auditStore);

        var invite = await handler.InviteAsync(
            new InviteAccountantFirmUserCommand(
                InviteeEmail: $"acc-{Guid.NewGuid():N}@nileaccounting.eg",
                InviteeDisplayName: new ArabicEnglishText("محاسب", "Accountant"),
                FirmName: "Nile Accounting LLC",
                FirmExternalIdentifier: "nileaccounting.eg",
                TemporaryPasswordHash: "argon2id$m=65536,t=3,p=4$XXXX$YYYY",
                InvitedByUserId: admin.Id
            ),
            CancellationToken.None
        );

        invite
            .AcceptedAtUtc.Should()
            .BeNull(because: "invitation just issued; awaiting accept handshake");
        invite.IsActive.Should().BeFalse();

        // Now the invitee accepts.
        var clock2 = new TestClock(new DateTime(2026, 5, 8, 10, 30, 0, DateTimeKind.Utc));
        var handler2 = new InviteAccountantFirmUserHandler(db, clock2, auditStore);
        var accepted = await handler2.AcceptAsync(
            new AcceptInvitationCommand(invite.UserId),
            CancellationToken.None
        );

        accepted
            .AcceptedAtUtc.Should()
            .Be(
                clock2.UtcNow,
                because: "Accept stamps the row's accepted_at_utc to the current clock"
            );
        accepted
            .IsActive.Should()
            .BeTrue(because: "FR-049 — accepted AND not revoked == active firm-user");

        // Audit log MUST contain firm_user.invited + firm_user.accepted
        // entries, both tagged with the firm name (US8 scenario 1).
        db.ChangeTracker.Clear();
        var entries = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .Where(e => e.Kind == "firm_user.invited" || e.Kind == "firm_user.accepted")
            .OrderBy(e => e.Index)
            .ToListAsync();
        entries.Should().HaveCount(2);
        entries[0].Kind.Should().Be("firm_user.invited");
        entries[0]
            .ActorFirmName.Should()
            .Be(
                "Nile Accounting LLC",
                because: "every firm-user-related audit row carries the firm name for inspector traceability"
            );
        entries[0]
            .ActorUserId.Should()
            .Be(admin.Id, because: "the inviter is the actor on the invite event");
        entries[1].Kind.Should().Be("firm_user.accepted");
        entries[1].ActorFirmName.Should().Be("Nile Accounting LLC");
        entries[1]
            .ActorUserId.Should()
            .Be(invite.UserId, because: "the invitee is the actor on the accept event");
    }

    [Fact]
    public async Task Accept_IsIdempotent_OriginalTimestampWins()
    {
        await using var db = await _fixture.CreateContextAsync();

        var admin = new User(
            email: $"admin-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مسؤول", "Administrator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(admin);
        await db.SaveChangesAsync();

        var firstAccept = new DateTime(2026, 5, 8, 10, 30, 0, DateTimeKind.Utc);
        var auditStore = new SqlAuditLogStore(db);
        var inviteHandler = new InviteAccountantFirmUserHandler(
            db,
            new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc)),
            auditStore
        );
        var firmUser = await inviteHandler.InviteAsync(
            new InviteAccountantFirmUserCommand(
                InviteeEmail: $"acc-{Guid.NewGuid():N}@nileaccounting.eg",
                InviteeDisplayName: new ArabicEnglishText("محاسب", "Accountant"),
                FirmName: "Nile Accounting LLC",
                FirmExternalIdentifier: "nileaccounting.eg",
                TemporaryPasswordHash: "argon2id$m=65536,t=3,p=4$XXXX$YYYY",
                InvitedByUserId: admin.Id
            ),
            CancellationToken.None
        );

        // First accept.
        await new InviteAccountantFirmUserHandler(
            db,
            new TestClock(firstAccept),
            auditStore
        ).AcceptAsync(new AcceptInvitationCommand(firmUser.UserId), CancellationToken.None);

        // Second accept at a later time MUST be a no-op — idempotent.
        // The original accept timestamp wins so the audit trail
        // remains authoritative (and we don't double-emit the
        // firm_user.accepted audit row).
        var secondClock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var refreshed = await new InviteAccountantFirmUserHandler(
            db,
            secondClock,
            auditStore
        ).AcceptAsync(new AcceptInvitationCommand(firmUser.UserId), CancellationToken.None);

        refreshed
            .AcceptedAtUtc.Should()
            .Be(firstAccept, because: "idempotent — original accept timestamp wins");

        db.ChangeTracker.Clear();
        var acceptedEntries = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .Where(e => e.Kind == "firm_user.accepted")
            .ToListAsync();
        acceptedEntries
            .Should()
            .HaveCount(1, because: "the second accept call MUST NOT emit a duplicate audit row");
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
