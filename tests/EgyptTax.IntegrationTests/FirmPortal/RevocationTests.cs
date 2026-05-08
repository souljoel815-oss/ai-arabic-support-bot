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
/// T219 / US8 scenario 4 / FR-049 — Administrator revokes a firm
/// user's scoped access. Effective immediately (the row's
/// IsActive flips to false on the same call); previous audit
/// entries + firm-user-tagged history remain readable in
/// perpetuity (FR-049 — append-only firm-user history). Revocation
/// is itself audit-logged with the firm name so an inspector can
/// trace "who revoked which firm-rep when".
/// </summary>
[Collection(SqlServerCollection.Name)]
public class RevocationTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Revoke_FlipsActive_WritesAuditRow_PreservesPriorHistory()
    {
        await using var db = await _fixture.CreateContextAsync();

        var admin = new User(
            email: $"admin-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مسؤول", "Administrator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(admin);
        await db.SaveChangesAsync();

        var auditStore = new SqlAuditLogStore(db);
        var inviteClock = new TestClock(new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc));
        var firmUser = await new InviteAccountantFirmUserHandler(db, inviteClock, auditStore)
            .InviteAsync(new InviteAccountantFirmUserCommand(
                InviteeEmail: $"acc-{Guid.NewGuid():N}@nileaccounting.eg",
                InviteeDisplayName: new ArabicEnglishText("محاسب", "Accountant"),
                FirmName: "Nile Accounting LLC",
                FirmExternalIdentifier: "nileaccounting.eg",
                TemporaryPasswordHash: "argon2id$m=65536,t=3,p=4$XXXX$YYYY",
                InvitedByUserId: admin.Id), CancellationToken.None);

        var acceptClock = new TestClock(new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc));
        await new InviteAccountantFirmUserHandler(db, acceptClock, auditStore)
            .AcceptAsync(new AcceptInvitationCommand(firmUser.UserId), CancellationToken.None);

        var auditEntriesBeforeRevoke = await db.Set<AuditLogEntry>().AsNoTracking()
            .Where(e => e.ActorFirmName == "Nile Accounting LLC")
            .ToListAsync();
        auditEntriesBeforeRevoke.Should().HaveCount(2,
            because: "two firm-tagged audit rows: invited + accepted");

        // Now revoke.
        var revokeClock = new TestClock(new DateTime(2026, 5, 8, 14, 0, 0, DateTimeKind.Utc));
        var revoked = await new InviteAccountantFirmUserHandler(db, revokeClock, auditStore)
            .RevokeAsync(new RevokeFirmUserCommand(firmUser.UserId, admin.Id), CancellationToken.None);

        revoked.RevokedAtUtc.Should().Be(revokeClock.UtcNow);
        revoked.RevokedByUserId.Should().Be(admin.Id);
        revoked.IsActive.Should().BeFalse(
            because: "revoked AccountantFirmUser is immediately inactive (FR-049 / US8 scenario 4)");

        // Prior audit history MUST still be present + readable.
        db.ChangeTracker.Clear();
        var allAfter = await db.Set<AuditLogEntry>().AsNoTracking()
            .Where(e => e.ActorFirmName == "Nile Accounting LLC")
            .OrderBy(e => e.Index)
            .ToListAsync();
        allAfter.Should().HaveCount(3,
            because: "the 2 prior + the new firm_user.revoked entry — append-only history");
        allAfter[0].Kind.Should().Be("firm_user.invited");
        allAfter[1].Kind.Should().Be("firm_user.accepted");
        allAfter[2].Kind.Should().Be("firm_user.revoked");
        allAfter[2].ActorUserId.Should().Be(admin.Id,
            because: "the revoker is the actor on the revoke event");
    }

    [Fact]
    public async Task Revoke_IsIdempotent_OriginalRevocationDataPreserved()
    {
        await using var db = await _fixture.CreateContextAsync();

        var admin = new User(
            email: $"admin-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مسؤول", "Administrator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        var admin2 = new User(
            email: $"admin2-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مسؤول٢", "Administrator2"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(admin); db.Add(admin2);
        await db.SaveChangesAsync();

        var auditStore = new SqlAuditLogStore(db);
        var firmUser = await new InviteAccountantFirmUserHandler(db,
                new TestClock(new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)), auditStore)
            .InviteAsync(new InviteAccountantFirmUserCommand(
                InviteeEmail: $"acc-{Guid.NewGuid():N}@nileaccounting.eg",
                InviteeDisplayName: new ArabicEnglishText("محاسب", "Accountant"),
                FirmName: "Nile Accounting LLC",
                FirmExternalIdentifier: "nileaccounting.eg",
                TemporaryPasswordHash: "argon2id$m=65536,t=3,p=4$XXXX$YYYY",
                InvitedByUserId: admin.Id), CancellationToken.None);
        await new InviteAccountantFirmUserHandler(db,
                new TestClock(new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc)), auditStore)
            .AcceptAsync(new AcceptInvitationCommand(firmUser.UserId), CancellationToken.None);

        var firstRevoke = new DateTime(2026, 5, 8, 14, 0, 0, DateTimeKind.Utc);
        await new InviteAccountantFirmUserHandler(db, new TestClock(firstRevoke), auditStore)
            .RevokeAsync(new RevokeFirmUserCommand(firmUser.UserId, admin.Id), CancellationToken.None);

        // Second revoke by a different admin at a later time MUST be
        // a no-op — original revocation data wins so the audit
        // chain stays authoritative + we don't double-emit the
        // firm_user.revoked row.
        var refreshed = await new InviteAccountantFirmUserHandler(db,
                new TestClock(new DateTime(2026, 5, 9, 14, 0, 0, DateTimeKind.Utc)), auditStore)
            .RevokeAsync(new RevokeFirmUserCommand(firmUser.UserId, admin2.Id), CancellationToken.None);

        refreshed.RevokedAtUtc.Should().Be(firstRevoke,
            because: "idempotent — original revoked_at_utc wins");
        refreshed.RevokedByUserId.Should().Be(admin.Id,
            because: "idempotent — original revoker wins");

        db.ChangeTracker.Clear();
        var revokeRows = await db.Set<AuditLogEntry>().AsNoTracking()
            .Where(e => e.Kind == "firm_user.revoked")
            .ToListAsync();
        revokeRows.Should().HaveCount(1,
            because: "the second revoke MUST NOT emit a duplicate audit row");
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
