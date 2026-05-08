using System.Security.Cryptography;
using System.Text;
using EgyptTax.Application.Attachments;
using EgyptTax.Application.Audit;
using EgyptTax.Application.FileStorage;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Attachments;
using EgyptTax.Infrastructure.FileStorage;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Attachments;

/// <summary>
/// T130 / FR-032 / R-21 — round-trip an upload through the
/// UploadAttachmentHandler against a real Testcontainers SQL +
/// real local-disk store, then assert:
///  * The metadata row's `sha256` column matches a fresh hash of
///    the original bytes.
///  * Reading the file back via the store yields the original bytes
///    intact.
///  * Detecting bit-rot is a one-line operation: re-hash the file
///    on disk, compare to the stored sha256, mismatch === rot.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AttachmentIntegrityTests(SqlServerFixture fixture) : IDisposable
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"egypttax-attach-{Guid.NewGuid():N}"
    );

    [Fact]
    public async Task Upload_RoundTrips_Sha256_AndContent_Bytes()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (parent, user) = await SeedParentAsync(db);

        var originalBytes = Encoding.UTF8.GetBytes(
            "%PDF-1.7\nfake but byte-stable content for hashing\n"
        );
        var expectedSha256 = SHA256.HashData(originalBytes);

        var handler = BuildHandler(db);
        await using var contentStream = new MemoryStream(originalBytes);
        var attachment = await handler.HandleAsync(
            new UploadAttachmentCommand(
                DocumentId: parent.Id,
                DocumentType: DocumentType.PurchaseInvoice,
                FilenameOriginal: "supplier-invoice.pdf",
                MimeType: "application/pdf",
                ContentStream: contentStream,
                UploadedByUserId: user.Id
            ),
            CancellationToken.None
        );

        // 1 — metadata SHA-256 matches a fresh hash of the bytes.
        attachment
            .Sha256.Should()
            .Equal(
                expectedSha256,
                because: "the store hashes during write; the metadata column MUST carry that hash so bit-rot detection can re-verify"
            );
        attachment.SizeBytes.Should().Be(originalBytes.Length);
        attachment.MimeType.Should().Be("application/pdf");
        attachment
            .RelativePath.Should()
            .StartWith(
                "attachments/",
                because: "R-21 layout: attachments/{yyyy}/{mm}/{document_id}/{attachment_id}.{ext}"
            );

        // 2 — content bytes round-trip through the store.
        var store = new FileSystemAttachmentStore(_tempRoot, new FixedClock(DateTime.UtcNow));
        await using var readBack = await store.OpenReadAsync(attachment.RelativePath);
        await using var sink = new MemoryStream();
        await readBack.CopyToAsync(sink);
        sink.ToArray()
            .Should()
            .Equal(
                originalBytes,
                because: "the persisted file MUST be byte-for-byte identical to the upload"
            );

        // 3 — re-hash the on-disk file and compare to the stored
        //     digest. This is exactly what the bit-rot scanner will
        //     do; proving the contract here ships the detection
        //     building block.
        await using var rehashStream = await store.OpenReadAsync(attachment.RelativePath);
        var diskHash = SHA256.HashData(ReadAll(rehashStream));
        diskHash
            .Should()
            .Equal(
                attachment.Sha256,
                because: "in the absence of bit-rot the on-disk hash MUST match the stored hash"
            );
    }

    [Fact]
    public async Task BitRot_Is_Detectable_By_Rehashing_The_OnDiskFile()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (parent, user) = await SeedParentAsync(db);

        var originalBytes = Encoding.UTF8.GetBytes("original content payload");
        var handler = BuildHandler(db);
        await using var contentStream = new MemoryStream(originalBytes);
        var attachment = await handler.HandleAsync(
            new UploadAttachmentCommand(
                parent.Id,
                DocumentType.PurchaseInvoice,
                "receipt.pdf",
                "application/pdf",
                contentStream,
                user.Id
            ),
            CancellationToken.None
        );

        // Simulate bit-rot: tamper the on-disk file directly.
        var absolutePath = Path.Combine(
            _tempRoot,
            attachment.RelativePath.Replace('/', Path.DirectorySeparatorChar)
        );
        var tampered = File.ReadAllBytes(absolutePath);
        tampered[5] ^= 0xFF; // flip a byte
        File.WriteAllBytes(absolutePath, tampered);

        // Bit-rot detection: re-hash + compare.
        var store = new FileSystemAttachmentStore(_tempRoot, new FixedClock(DateTime.UtcNow));
        await using var stream = await store.OpenReadAsync(attachment.RelativePath);
        var diskHash = SHA256.HashData(ReadAll(stream));

        diskHash
            .Should()
            .NotEqual(
                attachment.Sha256,
                because: "tampered bytes MUST hash to a different SHA-256 — the diff is the alarm"
            );
    }

    [Fact]
    public async Task Upload_Rejects_Disallowed_Mime_Type()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (parent, user) = await SeedParentAsync(db);
        var handler = BuildHandler(db);

        await using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes("any"));
        var act = async () =>
            await handler.HandleAsync(
                new UploadAttachmentCommand(
                    parent.Id,
                    DocumentType.PurchaseInvoice,
                    "evil.exe",
                    "application/x-msdownload",
                    contentStream,
                    user.Id
                ),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-032", StringComparison.OrdinalIgnoreCase));
    }

    private UploadAttachmentHandler BuildHandler(AppDbContext db)
    {
        var clock = new FixedClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_tempRoot, clock);
        return new UploadAttachmentHandler(db, store, new CaptureAuditLogStore(), clock);
    }

    private static async Task<(PurchaseInvoice, EgyptTax.Domain.Identity.User)> SeedParentAsync(
        AppDbContext db
    )
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
        var user = new EgyptTax.Domain.Identity.User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-1",
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(100m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent,
            deductibleFlag: false
        );

        db.Add(vat);
        db.Add(supplier);
        db.Add(user);
        db.Add(draft);
        await db.SaveChangesAsync();
        return (draft, user);
    }

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
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
            return Task.FromResult(
                new AuditLogEntry(
                    index: Captured.Count,
                    tsUtc: DateTime.UtcNow,
                    actorUserId: payload.ActorUserId,
                    actorFirmName: payload.ActorFirmName,
                    companyId: payload.CompanyId,
                    kind: payload.Kind,
                    payloadJson: payload.PayloadJson,
                    prevHash: new byte[32],
                    thisHash: new byte[32]
                )
            );
        }
    }
}
