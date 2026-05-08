using EgyptTax.Application.FirmPortal;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.FirmPortal;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.FirmPortal;

/// <summary>
/// T218 / US8 scenarios 3 + 4 / FR-050 — soft "lock-for-review"
/// handshake. Pins the entity + handler contract: bookkeeper locks
/// a month for review, accountant records adjusting actions during
/// the lock window, either party releases. The downstream
/// post-time guards (block bookkeeper edits + allow accountant JV
/// posts when an active lock covers the doc-date) are wired into
/// the Post*Handler pipelines as a follow-up; this test pins the
/// state-machine the guards key off.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PeriodReviewLockTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task LockReleaseRoundTrip_EmitsAuditRows_AndStampsLifecycleFields()
    {
        await using var db = await _fixture.CreateContextAsync();

        var bookkeeper = SeedUser(db, "bookkeeper");
        var accountant = SeedUser(db, "accountant");
        await db.SaveChangesAsync();

        var auditStore = new SqlAuditLogStore(db);
        var lockClock = new TestClock(new DateTime(2026, 5, 8, 16, 0, 0, DateTimeKind.Utc));
        var locked = await new PeriodReviewLockHandler(db, lockClock, auditStore)
            .LockAsync(new LockForReviewCommand(
                PeriodYear: 2026, PeriodMonth: 4,
                LockedByUserId: bookkeeper.Id,
                Note: "April books closed — please review before VAT filing"),
                CancellationToken.None);

        locked.IsActive.Should().BeTrue();
        locked.LockedAtUtc.Should().Be(lockClock.UtcNow);
        locked.LockedByUserId.Should().Be(bookkeeper.Id);
        locked.AccountantActionsDuringLock.Should().Be(0,
            because: "no accountant actions recorded yet");

        // Accountant records 3 adjusting actions on the lock —
        // the counter increments and the row stays Active.
        for (var i = 0; i < 3; i++)
        {
            locked.RecordAccountantAction();
        }
        await db.SaveChangesAsync();

        // Release (by the accountant).
        var releaseClock = new TestClock(new DateTime(2026, 5, 9, 11, 30, 0, DateTimeKind.Utc));
        var released = await new PeriodReviewLockHandler(db, releaseClock, auditStore)
            .ReleaseAsync(new ReleaseReviewCommand(locked.Id, accountant.Id), CancellationToken.None);

        released.IsActive.Should().BeFalse(
            because: "release flips the lock to inactive — review is over");
        released.ReleasedAtUtc.Should().Be(releaseClock.UtcNow);
        released.ReleasedByUserId.Should().Be(accountant.Id);
        released.AccountantActionsDuringLock.Should().Be(3,
            because: "the counter snapshot is preserved on release for the activity report");

        // Audit entries: period_review.locked + period_review.released.
        db.ChangeTracker.Clear();
        var entries = await db.Set<AuditLogEntry>().AsNoTracking()
            .Where(e => e.Kind == "period_review.locked" || e.Kind == "period_review.released")
            .OrderBy(e => e.Index)
            .ToListAsync();
        entries.Should().HaveCount(2);
        entries[0].Kind.Should().Be("period_review.locked");
        entries[0].ActorUserId.Should().Be(bookkeeper.Id);
        entries[1].Kind.Should().Be("period_review.released");
        entries[1].ActorUserId.Should().Be(accountant.Id);
        entries[1].PayloadJson.Should().Contain("\"accountant_actions_during_lock\":3",
            because: "the released audit row carries the activity counter snapshot");
    }

    [Fact]
    public async Task SecondActiveLock_OnSameMonth_IsRefused()
    {
        await using var db = await _fixture.CreateContextAsync();

        var bookkeeper = SeedUser(db, "bk");
        var bookkeeper2 = SeedUser(db, "bk2");
        await db.SaveChangesAsync();

        var auditStore = new SqlAuditLogStore(db);
        var clock = new TestClock(new DateTime(2026, 5, 8, 16, 0, 0, DateTimeKind.Utc));
        var handler = new PeriodReviewLockHandler(db, clock, auditStore);

        await handler.LockAsync(new LockForReviewCommand(2026, 4, bookkeeper.Id), CancellationToken.None);

        // Second lock attempt on the same (year, month) MUST fail.
        var act = async () =>
            await handler.LockAsync(new LockForReviewCommand(2026, 4, bookkeeper2.Id), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already locked for review*",
                because: "FR-050 — at most one active review-lock per (year, month)");
    }

    [Fact]
    public async Task ReleaseThenReLock_OnSameMonth_Succeeds_ViaFreshRow()
    {
        await using var db = await _fixture.CreateContextAsync();

        var bookkeeper = SeedUser(db, "bk");
        var accountant = SeedUser(db, "acc");
        await db.SaveChangesAsync();

        var auditStore = new SqlAuditLogStore(db);
        var firstLock = await new PeriodReviewLockHandler(db,
                new TestClock(new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc)), auditStore)
            .LockAsync(new LockForReviewCommand(2026, 4, bookkeeper.Id), CancellationToken.None);

        await new PeriodReviewLockHandler(db,
                new TestClock(new DateTime(2026, 5, 2, 9, 0, 0, DateTimeKind.Utc)), auditStore)
            .ReleaseAsync(new ReleaseReviewCommand(firstLock.Id, accountant.Id), CancellationToken.None);

        // After release, a fresh lock for the same month MUST succeed —
        // history rows for the same (year, month) are fine; only ONE
        // ACTIVE row at a time is enforced via the filtered unique index.
        var secondLock = await new PeriodReviewLockHandler(db,
                new TestClock(new DateTime(2026, 5, 3, 9, 0, 0, DateTimeKind.Utc)), auditStore)
            .LockAsync(new LockForReviewCommand(2026, 4, bookkeeper.Id), CancellationToken.None);
        secondLock.Id.Should().NotBe(firstLock.Id, because: "fresh row, not a re-activation of the prior lock");
        secondLock.IsActive.Should().BeTrue();

        // History view: both rows for (2026, 4) exist.
        db.ChangeTracker.Clear();
        var history = await db.Set<PeriodReviewLock>().AsNoTracking()
            .Where(l => l.PeriodYear == 2026 && l.PeriodMonth == 4)
            .OrderBy(l => l.LockedAtUtc)
            .ToListAsync();
        history.Should().HaveCount(2);
        history[0].ReleasedAtUtc.Should().NotBeNull();
        history[1].ReleasedAtUtc.Should().BeNull(because: "the new active lock is the open one");
    }

    private static User SeedUser(Microsoft.EntityFrameworkCore.DbContext db, string label)
    {
        var u = new User(
            email: $"{label}-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText(label, label),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(u);
        return u;
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
