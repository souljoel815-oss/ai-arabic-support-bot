using EgyptTax.Application.Audit;
using EgyptTax.Application.Expenses;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Expenses;

/// <summary>
/// FR-016 mirrored on the Expense surface — same invariant as
/// PurchaseInvoice (T128) but on the expense-document path. Ships
/// the buy-side guarantee end-to-end across both deductible
/// surfaces (PurchaseInvoice + Expense) so an operator can't sneak
/// an unsupported deduction in by routing through the simpler
/// Expense path.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ExpensePostTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Post_DeductibleExpense_With_NoAttachments_IsRejected()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (category, user) = await SeedAsync(db);

        var draft = Expense.CreateDraft(
            new DateOnly(2026, 5, 7), category.Id, MoneyEgp.From(500m),
            deductibleFlag: true,
            description: new ArabicEnglishText("مصروفات مكتبية", "Office supplies"));
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var act = async () => await handler.HandleAsync(
            new PostExpenseCommand(draft.Id, user.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-016", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("deductible", StringComparison.OrdinalIgnoreCase));

        var refreshed = await db.Set<Expense>().AsNoTracking().FirstAsync(e => e.Id == draft.Id);
        refreshed.State.Should().Be(DocumentState.Draft);
        refreshed.DocumentNumber.Should().BeNull();
    }

    [Fact]
    public async Task Post_DeductibleExpense_With_Attachment_Succeeds()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (category, user) = await SeedAsync(db);

        var draft = Expense.CreateDraft(
            new DateOnly(2026, 5, 7), category.Id, MoneyEgp.From(500m),
            deductibleFlag: true,
            description: new ArabicEnglishText("مصروفات مكتبية", "Office supplies"));
        db.Add(draft);

        var attachment = new Attachment(
            documentId: draft.Id,
            documentType: DocumentType.Expense,
            filenameOriginal: "receipt.pdf",
            filenameStorage: $"{Guid.NewGuid():N}.pdf",
            relativePath: $"attachments/2026/05/{draft.Id:D}/receipt.pdf",
            sha256: new byte[32],
            mimeType: "application/pdf",
            sizeBytes: 1234,
            uploadedByUserId: user.Id,
            uploadedAtUtc: new DateTime(2026, 5, 7, 9, 0, 0, DateTimeKind.Utc));
        db.Add(attachment);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var posted = await handler.HandleAsync(
            new PostExpenseCommand(draft.Id, user.Id), CancellationToken.None);

        posted.State.Should().Be(DocumentState.Posted);
        posted.DocumentNumber.Should().NotBeNullOrWhiteSpace(
            because: "FR-011 — sequential EXP-{year}-{n} allocated on Post");
    }

    [Fact]
    public async Task Post_NonDeductibleExpense_With_NoAttachments_Succeeds()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (category, user) = await SeedAsync(db);

        var draft = Expense.CreateDraft(
            new DateOnly(2026, 5, 7), category.Id, MoneyEgp.From(200m),
            deductibleFlag: false,
            description: new ArabicEnglishText("ضيافة", "Entertainment (non-deductible)"));
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db);
        var posted = await handler.HandleAsync(
            new PostExpenseCommand(draft.Id, user.Id), CancellationToken.None);

        posted.State.Should().Be(DocumentState.Posted);
    }

    private static PostExpenseHandler BuildHandler(AppDbContext db) =>
        new(db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore());

    private static async Task<(DeductibleExpenseCategory category, User user)> SeedAsync(AppDbContext db)
    {
        var category = new DeductibleExpenseCategory(
            code: $"CAT-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("فئة تجريبية", "Test Category"),
            defaultDeductible: true,
            defaultAccountId: Guid.NewGuid());
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(category); db.Add(user);
        await db.SaveChangesAsync();
        return (category, user);
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
