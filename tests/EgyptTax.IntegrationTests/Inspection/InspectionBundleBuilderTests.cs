using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EgyptTax.Application.Audit;
using EgyptTax.Application.FileStorage;
using EgyptTax.Application.Inspection;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.FileStorage;
using EgyptTax.Infrastructure.Inspection;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Inspection;

/// <summary>
/// US9 / FR-048 — happy-path bundle build + drafts-excluded
/// scenario per US9 acceptance scenario 3 (T229). Verifies the
/// manifest carries a coherent file list, every per-file SHA-256
/// matches the actual file bytes, and the audit-trail extract
/// covers the period.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class InspectionBundleBuilderTests(SqlServerFixture fixture) : IDisposable
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"egypttax-bundle-{Guid.NewGuid():N}"
    );

    [Fact]
    public async Task HappyPath_Bundle_CarriesManifestAndAttachments_AndPerFileHashesMatch()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedAsync(db);
        await EnsureCompanyAsync(db);

        // Post a deductible purchase invoice with one attachment so
        // the bundle has something interesting to package.
        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-1",
            new DateOnly(2026, 5, 5)
        );
        draft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(100m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent,
            deductibleFlag: true
        );
        db.Add(draft);
        var attachmentBytes = Encoding.UTF8.GetBytes(
            "%PDF-1.7\nfake supplier invoice for hashing\n"
        );
        var sha256 = SHA256.HashData(attachmentBytes);
        var store = new FileSystemAttachmentStore(_tempRoot, clock);
        await using var contentStream = new MemoryStream(attachmentBytes);
        var saved = await store.SaveAsync(draft.Id, Guid.NewGuid(), ".pdf", contentStream);
        db.Add(
            new Attachment(
                documentId: draft.Id,
                documentType: DocumentType.PurchaseInvoice,
                filenameOriginal: "supplier-1.pdf",
                filenameStorage: Path.GetFileName(saved.RelativePath),
                relativePath: saved.RelativePath,
                sha256: saved.ContentSha256,
                mimeType: "application/pdf",
                sizeBytes: saved.SizeBytes,
                uploadedByUserId: user.Id,
                uploadedAtUtc: new DateTime(2026, 5, 5, 9, 0, 0, DateTimeKind.Utc)
            )
        );
        await db.SaveChangesAsync();

        var auditStore = new SqlAuditLogStore(db);
        var postHandler = new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            auditStore
        );
        await postHandler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );

        // Build the bundle.
        var builder = new InspectionBundleBuilder(
            db,
            store,
            clock,
            new EgyptTax.Infrastructure.Reports.SqlTrialBalanceReportQuery(db)
        );
        var result = await builder.BuildAsync(
            new InspectionBundleRequest(
                PeriodStart: new DateOnly(2026, 5, 1),
                PeriodEnd: new DateOnly(2026, 5, 31),
                GeneratedByUserId: user.Id,
                AllowDrafts: false
            ),
            CancellationToken.None
        );

        // Manifest sanity.
        result.Manifest.BundleVersion.Should().Be("1.0");
        result
            .Manifest.Files.Should()
            .Contain(f =>
                f.RelativePath == "MANIFEST.sha256" || f.Category == "ReadmeForInspector"
            );
        result
            .Manifest.Files.Should()
            .Contain(
                f => f.Category == "Attachment",
                because: "the deductible-purchase attachment MUST appear in the bundle"
            );
        result.Manifest.Files.Should().Contain(f => f.Category == "AuditTrailExtract");
        result.Manifest.Files.Should().Contain(f => f.Category == "ReadmeForInspector");
        result.Manifest.DraftsExcluded.Should().BeFalse();
        result.Manifest.TopLevelArchiveSha256.Should().NotBeNullOrEmpty();

        // Open the produced ZIP and verify per-file hashes.
        await using var zipStream = new MemoryStream(result.ZipBytes);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = zip.GetEntry("MANIFEST.sha256")!;
        await using var manifestStream = manifestEntry.Open();
        using var manifestReader = new StreamReader(manifestStream);
        var manifestBody = await manifestReader.ReadToEndAsync();
        var parsed = JsonDocument.Parse(manifestBody);
        parsed.RootElement.GetProperty("bundleVersion").GetString().Should().Be("1.0");

        // Each file listed in the manifest MUST exist in the ZIP and
        // hash to the manifest's recorded sha256.
        foreach (var file in result.Manifest.Files)
        {
            var entry = zip.GetEntry(file.RelativePath);
            entry
                .Should()
                .NotBeNull(because: $"{file.RelativePath} listed in manifest but missing from ZIP");
            await using var s = entry!.Open();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
#pragma warning disable CA1308 // Manifest schema requires lowercase hex.
            var hashHex = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
#pragma warning restore CA1308
            hashHex
                .Should()
                .Be(
                    file.Sha256,
                    because: $"the manifest's sha256 for {file.RelativePath} MUST match the actual file bytes — this is the bedrock invariant the inspector verifies"
                );
        }

        // Attachment file bytes MUST be byte-for-byte identical to
        // the original upload.
        var attachmentEntries = zip
            .Entries.Where(e => e.FullName.StartsWith("attachments/", StringComparison.Ordinal))
            .ToList();
        attachmentEntries.Should().HaveCount(1);
        await using var attachStream = attachmentEntries[0].Open();
        using var attachMs = new MemoryStream();
        await attachStream.CopyToAsync(attachMs);
        attachMs
            .ToArray()
            .Should()
            .Equal(
                attachmentBytes,
                because: "the bundle MUST carry the original attachment bytes verbatim — modification = invalid bundle"
            );
    }

    [Fact]
    public async Task DraftsInPeriod_Without_AllowDrafts_Throws()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedAsync(db);
        await EnsureCompanyAsync(db);

        // Create a draft in the period (do NOT post it).
        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-DRAFT",
            new DateOnly(2026, 6, 5)
        );
        draft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(50m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent,
            deductibleFlag: false
        );
        db.Add(draft);
        await db.SaveChangesAsync();

        var builder = new InspectionBundleBuilder(
            db,
            new FileSystemAttachmentStore(_tempRoot, new TestClock(DateTime.UtcNow)),
            new TestClock(DateTime.UtcNow),
            new EgyptTax.Infrastructure.Reports.SqlTrialBalanceReportQuery(db)
        );

        var act = async () =>
            await builder.BuildAsync(
                new InspectionBundleRequest(
                    PeriodStart: new DateOnly(2026, 6, 1),
                    PeriodEnd: new DateOnly(2026, 6, 30),
                    GeneratedByUserId: user.Id,
                    AllowDrafts: false
                ),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(
                ex => ex.Message.Contains("draft", StringComparison.OrdinalIgnoreCase),
                because: "US9 acceptance scenario 3 — bundle MUST refuse to build when drafts in period are present unless the operator explicitly accepts"
            );
    }

    [Fact]
    public async Task DraftsInPeriod_With_AllowDrafts_RecordsExcludedIds_InManifest()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedAsync(db);
        await EnsureCompanyAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-DRAFT-2",
            new DateOnly(2026, 7, 8)
        );
        draft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(50m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent,
            deductibleFlag: false
        );
        db.Add(draft);
        await db.SaveChangesAsync();

        var builder = new InspectionBundleBuilder(
            db,
            new FileSystemAttachmentStore(_tempRoot, new TestClock(DateTime.UtcNow)),
            new TestClock(DateTime.UtcNow),
            new EgyptTax.Infrastructure.Reports.SqlTrialBalanceReportQuery(db)
        );
        var result = await builder.BuildAsync(
            new InspectionBundleRequest(
                PeriodStart: new DateOnly(2026, 7, 1),
                PeriodEnd: new DateOnly(2026, 7, 31),
                GeneratedByUserId: user.Id,
                AllowDrafts: true
            ),
            CancellationToken.None
        );

        result.Manifest.DraftsExcluded.Should().BeTrue();
        result.Manifest.ExcludedDraftIds.Should().NotBeNull();
        result
            .Manifest.ExcludedDraftIds!.Should()
            .Contain(
                draft.Id,
                because: "the operator-acknowledged drafts MUST appear in the manifest so the inspector knows what was omitted"
            );
    }

    private static async Task<(Supplier, VatCategory, User)> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                EgyptianTin.Parse("123456789"),
                vat.Id
            )
        );
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(vat);
        db.Add(supplier);
        db.Add(user);
        await db.SaveChangesAsync();
        return (supplier, vat, user);
    }

    private static async Task EnsureCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync())
            return;
        db.Add(
            new Company(
                legalName: new ArabicEnglishText("شركة", "Test Company SAE"),
                taxRegistrationNumber: EgyptianTin.Parse("123456789"),
                commercialRegistrationNumber: "CR-1",
                address: PostalAddress.Create(
                    new ArabicEnglishText("القاهرة", "Cairo"),
                    "Cairo",
                    "Downtown",
                    "Tahrir",
                    "12",
                    postalCode: "11511"
                ),
                taxpayerActivityCode: "0001"
            )
        );
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
