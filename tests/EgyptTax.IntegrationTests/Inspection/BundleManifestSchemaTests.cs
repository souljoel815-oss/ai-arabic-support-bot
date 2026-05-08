using System.IO.Compression;
using System.Text.Json.Nodes;
using EgyptTax.Application.Inspection;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.FileStorage;
using EgyptTax.Infrastructure.Inspection;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Json.Schema;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Inspection;

/// <summary>
/// T227 — every generated inspection-bundle MANIFEST.sha256 MUST
/// validate against
/// <c>contracts/inspection-bundle-manifest.schema.json</c>. This
/// catches accidental field renames + type drift in CI before they
/// ship as silently-broken bundles.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BundleManifestSchemaTests(SqlServerFixture fixture) : IDisposable
{
    private static readonly JsonSchema Schema = LoadSchema();

    private readonly SqlServerFixture _fixture = fixture;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"egypttax-bundle-schema-{Guid.NewGuid():N}"
    );

    [Fact]
    public async Task GeneratedManifest_Validates_AgainstContractSchema()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(user);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_tempRoot, clock);
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

        // Pull MANIFEST.sha256 out of the produced ZIP — that's
        // the bytes the inspector + the contract schema validate
        // against.
        await using var zipStream = new MemoryStream(result.ZipBytes);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = zip.GetEntry("MANIFEST.sha256")!;
        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        var manifestJson = await reader.ReadToEndAsync();

        var node = JsonNode.Parse(manifestJson);
        var validation = Schema.Evaluate(
            node,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                EvaluateAs = SpecVersion.Draft202012,
            }
        );

        if (!validation.IsValid)
        {
            var errors = validation
                .Details.Where(d => d.HasErrors)
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key}={e.Value}"))
                .ToArray();
            Assert.Fail("Manifest failed schema validation: " + string.Join("; ", errors));
        }

        // Spot-check a couple of must-have fields the schema marks
        // required so a regression that drops them surfaces clearly.
        node!["bundleVersion"]!.GetValue<string>().Should().Be("1.0");
        node["files"]!.AsArray().Count.Should().BeGreaterThan(0);
        node["topLevelArchiveSha256"]!.GetValue<string>().Should().MatchRegex("^[a-f0-9]{64}$");
        node["auditChainExtract"]!["extractSha256"]!
            .GetValue<string>()
            .Should()
            .MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    public async Task DraftsExcluded_ManifestStillValidates()
    {
        // The optional excludedDraftIds field flips the schema branch;
        // make sure we don't break validity when drafts are present.
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);

        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
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
        var draft = EgyptTax.Domain.Purchases.PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-DRAFT",
            new DateOnly(2026, 5, 10)
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
        db.Add(vat);
        db.Add(user);
        db.Add(supplier);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_tempRoot, clock);
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
                AllowDrafts: true
            ),
            CancellationToken.None
        );

        await using var zipStream = new MemoryStream(result.ZipBytes);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = zip.GetEntry("MANIFEST.sha256")!;
        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        var manifestJson = await reader.ReadToEndAsync();

        var node = JsonNode.Parse(manifestJson);
        var validation = Schema.Evaluate(
            node,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                EvaluateAs = SpecVersion.Draft202012,
            }
        );

        validation
            .IsValid.Should()
            .BeTrue(
                because: "the drafts-excluded branch (with excludedDraftIds populated) MUST also satisfy the schema"
            );
        node!["draftsExcluded"]!.GetValue<bool>().Should().BeTrue();
        node["excludedDraftIds"]!.AsArray().Count.Should().BeGreaterThanOrEqualTo(1);
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

    private static JsonSchema LoadSchema()
    {
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "inspection-bundle-manifest.schema.json"
        );
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException(
                $"Bundle manifest schema not found at {schemaPath}. The .csproj must copy it via the contracts/ glob."
            );
        }
        return JsonSchema.FromFile(schemaPath);
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
