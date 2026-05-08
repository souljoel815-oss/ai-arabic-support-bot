using EgyptTax.Application.Audit;
using EgyptTax.Application.Journals;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Journals;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Journals;

/// <summary>
/// T167a / FR-031 / Round-6 F9 — only Administrator + Accountant
/// can create manual adjusting journal vouchers; Bookkeeper is the
/// canonical rejected role. The handler emits a
/// <c>journal_voucher.manual.permission_denied</c> audit entry on
/// rejection so an inspector can see the attempt + the held roles.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ManualJournalRolePermissionTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Accountant")]
    public async Task User_With_PermittedRole_CanCreateManualVoucher(string roleCode)
    {
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserWithRolesAsync(db, roleCode);
        var handler = BuildHandler(db);

        var voucher = await handler.HandleAsync(
            BalancedCommand(user.Id), CancellationToken.None);

        voucher.Should().NotBeNull(because: $"FR-031 — {roleCode} MUST be allowed to create manual adjusting journals");
        var persistedCount = await db.Set<JournalVoucher>().AsNoTracking().CountAsync();
        persistedCount.Should().Be(1);
    }

    [Fact]
    public async Task Bookkeeper_Cannot_CreateManualVoucher_AndRejectionIsAudited()
    {
        await using var db = await _fixture.CreateContextAsync();
        var bookkeeper = await SeedUserWithRolesAsync(db, "Bookkeeper");
        var audit = new CaptureAuditLogStore();
        var handler = new CreateManualAdjustingJournalHandler(db,
            new TestClock(new DateTime(2026, 5, 8, 11, 0, 0, DateTimeKind.Utc)),
            audit);

        var act = async () => await handler.HandleAsync(
            BalancedCommand(bookkeeper.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .Where(ex => ex.Message.Contains("FR-031", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("Bookkeeper", StringComparison.OrdinalIgnoreCase));

        // No voucher persisted.
        var voucherCount = await db.Set<JournalVoucher>().AsNoTracking().CountAsync();
        voucherCount.Should().Be(0,
            because: "FR-031 — a permission-denied rejection MUST happen BEFORE the voucher is constructed");

        // The rejection itself is an auditable event.
        audit.Captured.Should().Contain(p => p.Kind == "journal_voucher.manual.permission_denied",
            because: "an inspector MUST be able to see attempted FR-031 violations in the audit chain");
    }

    [Fact]
    public async Task User_With_OnlyApproverRole_Cannot_CreateManualVoucher()
    {
        // Approver is allowed to approve documents but not to create
        // manual GL adjustments — that's still an Accountant /
        // Administrator privilege per FR-031.
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserWithRolesAsync(db, "Approver");
        var handler = BuildHandler(db);

        var act = async () => await handler.HandleAsync(
            BalancedCommand(user.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task User_With_MultipleRoles_IncludingPermitted_CanCreateManualVoucher()
    {
        // A user who holds Bookkeeper + Accountant should pass — the
        // gate is "ANY held role is permitted", not "ALL".
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserWithRolesAsync(db, "Bookkeeper", "Accountant");
        var handler = BuildHandler(db);

        var voucher = await handler.HandleAsync(
            BalancedCommand(user.Id), CancellationToken.None);

        voucher.Should().NotBeNull();
    }

    private static CreateManualAdjustingJournalCommand BalancedCommand(Guid userId) =>
        new(
            Date: new DateOnly(2026, 5, 8),
            Narration: new ArabicEnglishText("اختبار", "Test voucher"),
            CreatedByUserId: userId,
            Lines: new[]
            {
                new ManualJournalLineInput("5200", MoneyEgp.From(500m), MoneyEgp.Zero, "Expense"),
                new ManualJournalLineInput("2200", MoneyEgp.Zero, MoneyEgp.From(500m), "Payable"),
            });

    private static CreateManualAdjustingJournalHandler BuildHandler(AppDbContext db) =>
        new(db,
            new TestClock(new DateTime(2026, 5, 8, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore());

    private static async Task<User> SeedUserWithRolesAsync(AppDbContext db, params string[] roleCodes)
    {
        var roles = roleCodes.Select(c => new Role(
            code: c, name: new ArabicEnglishText(c, c), requiresMfa: false)).ToList();
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        foreach (var r in roles) { user.Roles.Add(r); db.Add(r); }
        db.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public List<AuditLogPayload> Captured { get; } = [];
        public Task<AuditLogEntry> AppendAsync(AuditLogPayload payload, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(payload);
            Captured.Add(payload);
            return Task.FromResult(new AuditLogEntry(
                index: Captured.Count, tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId, actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId, kind: payload.Kind, payloadJson: payload.PayloadJson,
                prevHash: new byte[32], thisHash: new byte[32]));
        }
    }
}
