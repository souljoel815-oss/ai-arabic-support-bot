using System.Diagnostics;
using EgyptTax.Application.FirmPortal;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.FirmPortal;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.FirmPortal;

/// <summary>
/// T217 / SC-014 (partial) / FR-049 — switching between two
/// installations MUST land in under 1 s. The full SC-014 covers
/// the browser-side credential pool (T222) + the per-installation
/// session cookie handshake (T223 CompanySwitcher) — both layered
/// in batch C UI work. This test covers the SERVER-SIDE leg of
/// the switch: given a firm-user with active rows in TWO
/// installations (we approximate by spinning two databases off the
/// shared container, mirroring the on-prem topology), the lookup
/// "is this user a firm user — and if so what's their firm
/// affiliation" MUST return in well under 1 s for both
/// installations. Pins the data-layer side of the switch perf
/// budget so any future regression on the AccountantFirmUser
/// lookup path is caught here even before the UI ships.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class CompanySwitchPerfTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task LookupFirmUser_AcrossTwoInstallations_CompletesUnder1Second()
    {
        // "Two installations" topology — each Egyptian SME runs its
        // own on-prem installation per FR-049 (no central vendor
        // hub). The firm user has a row in EACH installation;
        // switching between them is a re-auth against installation
        // B + a fresh AccountantFirmUser lookup. We approximate that
        // here by creating two independent DB contexts (one per
        // installation) and timing the lookup roundtrip against both.
        await using var dbA = await _fixture.CreateContextAsync();
        await using var dbB = await _fixture.CreateContextAsync();

        // Seed identical firm-user rows in both installations.
        var auditA = new SqlAuditLogStore(dbA);
        var auditB = new SqlAuditLogStore(dbB);
        var firmEmail = $"acc-{Guid.NewGuid():N}@nileaccounting.eg";
        var firmExtId = "nileaccounting.eg";

        var adminA = SeedAdmin(dbA);
        var adminB = SeedAdmin(dbB);
        await dbA.SaveChangesAsync();
        await dbB.SaveChangesAsync();

        var firmUserA = await new InviteAccountantFirmUserHandler(dbA,
                new TestClock(new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)), auditA)
            .InviteAsync(new InviteAccountantFirmUserCommand(
                InviteeEmail: firmEmail,
                InviteeDisplayName: new ArabicEnglishText("محاسب", "Accountant"),
                FirmName: "Nile Accounting LLC",
                FirmExternalIdentifier: firmExtId,
                TemporaryPasswordHash: "argon2id$m=65536,t=3,p=4$XXXX$YYYY",
                InvitedByUserId: adminA.Id), CancellationToken.None);
        var firmUserB = await new InviteAccountantFirmUserHandler(dbB,
                new TestClock(new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)), auditB)
            .InviteAsync(new InviteAccountantFirmUserCommand(
                InviteeEmail: firmEmail,
                InviteeDisplayName: new ArabicEnglishText("محاسب", "Accountant"),
                FirmName: "Nile Accounting LLC",
                FirmExternalIdentifier: firmExtId,
                TemporaryPasswordHash: "argon2id$m=65536,t=3,p=4$XXXX$YYYY",
                InvitedByUserId: adminB.Id), CancellationToken.None);

        // Warm — first run includes JIT + EF model build cost.
        _ = await LookupAsync(dbA, firmExtId);
        _ = await LookupAsync(dbB, firmExtId);

        // Now time the switch: lookup against B then A then B again.
        var sw = Stopwatch.StartNew();
        var first = await LookupAsync(dbA, firmExtId);
        var second = await LookupAsync(dbB, firmExtId);
        var third = await LookupAsync(dbA, firmExtId);
        sw.Stop();

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        third.Should().NotBeNull();
        first!.UserId.Should().Be(firmUserA.UserId);
        second!.UserId.Should().Be(firmUserB.UserId);

        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            because: "SC-014 — server-side firm-user lookup across two installations "
                + "MUST land well under 1 s (we did THREE roundtrips inside the budget; "
                + $"actual was {sw.ElapsedMilliseconds} ms)");
    }

    private static Task<AccountantFirmUser?> LookupAsync(Microsoft.EntityFrameworkCore.DbContext db, string firmExternalIdentifier)
    {
        return db.Set<AccountantFirmUser>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.FirmExternalIdentifier == firmExternalIdentifier);
    }

    private static User SeedAdmin(Microsoft.EntityFrameworkCore.DbContext db)
    {
        var u = new User(
            email: $"admin-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مسؤول", "Administrator"),
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
