using EgyptTax.Application.Audit;
using EgyptTax.Application.FixedAssets;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.FixedAssets;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.FixedAssets;

/// <summary>
/// T187 — query + create handler that the fixed-asset Razor pages
/// (FixedAssetList / FixedAssetEdit / FixedAssetSchedule) sit on.
/// Pins the operator-visible numbers: per-row NBV at as-of date,
/// attachment count for the badge, code uniqueness on create.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class FixedAssetQueryTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task ListAsync_ReturnsRows_WithComputedNbv_AndAttachmentCount()
    {
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserAsync(db);

        // Asset A — InService since Jan 2026, 60k cost, 60-month life
        // (1000/month). NBV at end of Mar 2026 = 60k − 3*1000 = 57k.
        var assetA = FixedAsset.CreateDraft(
            code: "FA-LIST-A", description: new ArabicEnglishText("معدات", "List asset A"),
            assetCategory: "Equipment", cost: MoneyEgp.From(60_000m),
            inServiceDate: new DateOnly(2026, 1, 1), usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero, convention: DepreciationConvention.FullMonth);
        assetA.PutInService();
        db.Add(assetA);
        // Two attachments to prove the count rolls up.
        for (var i = 0; i < 2; i++)
        {
            db.Add(new Attachment(
                documentId: assetA.Id, documentType: DocumentType.FixedAsset,
                filenameOriginal: $"doc-{i}.pdf",
                filenameStorage: $"{Guid.NewGuid():N}.pdf",
                relativePath: $"attachments/2026/01/{assetA.Id:D}/doc-{i}.pdf",
                sha256: new byte[32], mimeType: "application/pdf",
                sizeBytes: 1024, uploadedByUserId: user.Id,
                uploadedAtUtc: new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)));
        }

        // Asset B — Draft, no attachments. NBV stays at cost.
        var assetB = FixedAsset.CreateDraft(
            code: "FA-LIST-B", description: new ArabicEnglishText("مسودة", "List asset B (draft)"),
            assetCategory: "Equipment", cost: MoneyEgp.From(12_000m),
            inServiceDate: new DateOnly(2026, 6, 1), usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero, convention: DepreciationConvention.FullMonth);
        db.Add(assetB);
        await db.SaveChangesAsync();

        var query = new SqlFixedAssetQuery(db);
        var rows = await query.ListAsync(asOf: new DateOnly(2026, 3, 31));

        rows.Should().HaveCount(2);

        var rowA = rows.Single(r => r.Code == "FA-LIST-A");
        rowA.Status.Should().Be(FixedAssetStatus.InService);
        rowA.AttachmentCount.Should().Be(2,
            because: "two attachments uploaded for asset A");
        rowA.NetBookValue.Amount.Should().Be(57_000m,
            because: "60k cost − 3 months × 1000 depreciation = 57k NBV at end of Mar 2026");
        rowA.DepreciatedToDate.Amount.Should().Be(3_000m);

        var rowB = rows.Single(r => r.Code == "FA-LIST-B");
        rowB.Status.Should().Be(FixedAssetStatus.Draft);
        rowB.AttachmentCount.Should().Be(0);
        rowB.NetBookValue.Amount.Should().Be(12_000m,
            because: "Draft assets aren't depreciable yet — NBV stays at cost");
    }

    [Fact]
    public async Task CreateFixedAsset_PersistsDraft_AndEmitsCreatedAuditEvent()
    {
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserAsync(db);

        var audit = new CaptureAuditLogStore();
        var handler = new CreateFixedAssetHandler(db,
            new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
            audit);

        var asset = await handler.HandleAsync(new CreateFixedAssetCommand(
            Code: "FA-NEW",
            Description: new ArabicEnglishText("سيارة", "Vehicle"),
            AssetCategory: "Vehicles",
            Cost: MoneyEgp.From(150_000m),
            InServiceDate: new DateOnly(2026, 5, 9),
            UsefulLifeMonths: 84,
            DepreciationMethod: DepreciationMethod.StraightLine,
            SalvageValue: MoneyEgp.From(15_000m),
            Convention: DepreciationConvention.FullMonth,
            CreatedByUserId: user.Id), CancellationToken.None);

        asset.Status.Should().Be(FixedAssetStatus.Draft,
            because: "create starts in Draft; PutInService transitions later");
        asset.Code.Should().Be("FA-NEW");

        db.ChangeTracker.Clear();
        var reloaded = await db.Set<FixedAsset>().AsNoTracking()
            .FirstAsync(a => a.Id == asset.Id);
        reloaded.Cost.Amount.Should().Be(150_000m);
        reloaded.SalvageValue.Amount.Should().Be(15_000m);
        reloaded.UsefulLifeMonths.Should().Be(84);

        audit.Captured.Should().Contain(p => p.Kind == "fixed_asset.created",
            because: "FR-028 — every aggregate creation is auditable");
    }

    [Fact]
    public async Task CreateFixedAsset_DuplicateCode_IsRejected()
    {
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserAsync(db);

        var handler = new CreateFixedAssetHandler(db,
            new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore());

        var first = new CreateFixedAssetCommand(
            Code: "FA-DUP",
            Description: new ArabicEnglishText("أصل", "Asset"),
            AssetCategory: "Equipment",
            Cost: MoneyEgp.From(10_000m),
            InServiceDate: new DateOnly(2026, 5, 9),
            UsefulLifeMonths: 60,
            DepreciationMethod: DepreciationMethod.StraightLine,
            SalvageValue: MoneyEgp.Zero,
            Convention: DepreciationConvention.FullMonth,
            CreatedByUserId: user.Id);
        await handler.HandleAsync(first, CancellationToken.None);

        // Second create with same code MUST be refused.
        var act = async () => await handler.HandleAsync(first, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("already in use", StringComparison.OrdinalIgnoreCase));

        var count = await db.Set<FixedAsset>().AsNoTracking()
            .CountAsync(a => a.Code == "FA-DUP");
        count.Should().Be(1, because: "exactly one FA-DUP row exists; the rejection prevented the second");
    }

    private static async Task<User> SeedUserAsync(AppDbContext db)
    {
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
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
