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
/// T182 / US6 scenario 3 — fixed-asset Draft → InService MUST be
/// blocked when no attachment is on file. Mirrors the FR-016
/// guarantee on the deductible-purchase + deductible-expense paths:
/// capital expenditures need supporting documentation an inspector
/// can validate the cost basis against. Pins both the rejection
/// path AND the happy-path (with attachment) so the gate isn't
/// rejecting everything.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AttachmentRequiredTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task PutInService_WithoutAttachment_IsRejected_AndAuditEventCaptured()
    {
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserAsync(db);
        var asset = SeedDraftAsset();
        db.Add(asset);
        await db.SaveChangesAsync();

        var audit = new CaptureAuditLogStore();
        var handler = new PutFixedAssetInServiceHandler(db,
            new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
            audit);

        var act = async () => await handler.HandleAsync(
            new PutFixedAssetInServiceCommand(asset.Id, user.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("attachment", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("FR-016", StringComparison.OrdinalIgnoreCase));

        // Asset MUST still be Draft.
        db.ChangeTracker.Clear();
        var refreshed = await db.Set<FixedAsset>().AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        refreshed.Status.Should().Be(FixedAssetStatus.Draft,
            because: "the rejection MUST happen BEFORE PutInService runs — failed transition keeps the aggregate in Draft");

        // Rejection MUST be auditable so an inspector can see attempts.
        audit.Captured.Should().Contain(p => p.Kind == "fixed_asset.put_in_service.rejected_missing_attachment");
    }

    [Fact]
    public async Task PutInService_WithAttachment_Succeeds_AndTransitionsToInService()
    {
        await using var db = await _fixture.CreateContextAsync();
        var user = await SeedUserAsync(db);
        var asset = SeedDraftAsset();
        db.Add(asset);

        // Attach the supporting documentation.
        db.Add(new Attachment(
            documentId: asset.Id,
            documentType: DocumentType.FixedAsset,
            filenameOriginal: "purchase-receipt.pdf",
            filenameStorage: $"{Guid.NewGuid():N}.pdf",
            relativePath: $"attachments/2026/05/{asset.Id:D}/purchase-receipt.pdf",
            sha256: new byte[32], mimeType: "application/pdf",
            sizeBytes: 4096, uploadedByUserId: user.Id,
            uploadedAtUtc: new DateTime(2026, 5, 9, 9, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var audit = new CaptureAuditLogStore();
        var handler = new PutFixedAssetInServiceHandler(db,
            new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
            audit);

        var inService = await handler.HandleAsync(
            new PutFixedAssetInServiceCommand(asset.Id, user.Id), CancellationToken.None);

        inService.Status.Should().Be(FixedAssetStatus.InService);

        db.ChangeTracker.Clear();
        var refreshed = await db.Set<FixedAsset>().AsNoTracking().FirstAsync(a => a.Id == asset.Id);
        refreshed.Status.Should().Be(FixedAssetStatus.InService);

        audit.Captured.Should().Contain(p => p.Kind == "fixed_asset.put_in_service");
    }

    private static FixedAsset SeedDraftAsset() =>
        FixedAsset.CreateDraft(
            code: $"FA-{Guid.NewGuid():N}".Substring(0, 12),
            description: new ArabicEnglishText("معدات", "Test machinery"),
            assetCategory: "Equipment",
            cost: MoneyEgp.From(100_000m),
            inServiceDate: new DateOnly(2026, 5, 9),
            usefulLifeMonths: 60,
            depreciationMethod: DepreciationMethod.StraightLine,
            salvageValue: MoneyEgp.Zero,
            convention: DepreciationConvention.FullMonth);

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
