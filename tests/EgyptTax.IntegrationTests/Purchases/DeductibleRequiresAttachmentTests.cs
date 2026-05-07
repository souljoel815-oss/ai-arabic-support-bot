using EgyptTax.Application.Audit;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Purchases;

/// <summary>
/// T128 / FR-016 — at-least-one-attachment is required when ANY
/// line on a purchase invoice carries the deductible flag. The
/// post handler is the enforcement point: counts attachments and
/// throws if a deductible line exists with zero attachments. Tests
/// against real Testcontainers SQL so the EF + state-machine +
/// numbering stack all participate.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class DeductibleRequiresAttachmentTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Post_Of_DeductiblePurchaseInvoice_With_NoAttachments_IsRejected()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, operatorUser) = await SeedAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id, supplier.TaxProfile, "SUP-INV-1001",
            new DateOnly(2026, 5, 7));
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(1_000m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: true);
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var act = async () => await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-016", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("deductible", StringComparison.OrdinalIgnoreCase));

        var refreshed = await db.Set<PurchaseInvoice>().AsNoTracking()
            .FirstAsync(p => p.Id == draft.Id);
        refreshed.State.Should().Be(DocumentState.Draft,
            because: "the rejection MUST happen before the state transition — the document number was never burned");
        refreshed.DocumentNumber.Should().BeNull();
    }

    [Fact]
    public async Task Post_Of_DeductiblePurchaseInvoice_With_AnAttachment_Succeeds()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, operatorUser) = await SeedAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id, supplier.TaxProfile, "SUP-INV-1002",
            new DateOnly(2026, 5, 7));
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(1_000m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: true);
        db.Add(draft);
        await db.SaveChangesAsync();

        // Persist a single attachment row pointing at this draft.
        var attachment = new Attachment(
            documentId: draft.Id,
            documentType: DocumentType.PurchaseInvoice,
            filenameOriginal: "receipt.pdf",
            filenameStorage: $"{Guid.NewGuid():N}.pdf",
            relativePath: $"attachments/2026/05/{draft.Id:D}/receipt.pdf",
            sha256: new byte[32],
            mimeType: "application/pdf",
            sizeBytes: 12_345,
            uploadedByUserId: operatorUser.Id,
            uploadedAtUtc: new DateTime(2026, 5, 7, 10, 0, 0, DateTimeKind.Utc));
        db.Add(attachment);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var posted = await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None);

        posted.State.Should().Be(DocumentState.Posted);
        posted.DocumentNumber.Should().NotBeNullOrWhiteSpace(
            because: "FR-011 — a sequential PI-{year}-{n} is allocated on Post");
    }

    [Fact]
    public async Task Post_Of_NonDeductiblePurchaseInvoice_With_NoAttachments_Succeeds()
    {
        // FR-016 only applies to lines marked deductible. A pure
        // non-deductible purchase is fine without attachments.
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, operatorUser) = await SeedAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id, supplier.TaxProfile, "SUP-INV-1003",
            new DateOnly(2026, 5, 7));
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(500m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: false);
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var posted = await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None);

        posted.State.Should().Be(DocumentState.Posted);
    }

    private static PostPurchaseInvoiceHandler BuildHandler(AppDbContext db) =>
        new(db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore());

    private static async Task<(Supplier supplier, VatCategory vat, User user)> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier LLC"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                EgyptianTin.Parse("123456789"), vat.Id));
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Operator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(supplier); db.Add(user);
        await db.SaveChangesAsync();
        return (supplier, vat, user);
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
