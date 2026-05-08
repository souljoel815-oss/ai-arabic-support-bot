using EgyptTax.Application.Audit;
using EgyptTax.Application.Expenses;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Journals;

/// <summary>
/// US4 / FR-014 — expense auto-emit closes the auto-emit story
/// (sales + purchase + expense all generate balanced journal
/// entries). Without this, expense documents were silently
/// missing from the trial balance + journal listing — an
/// inspector reading the bundle would see expenses in the
/// purchase-and-expense register but no corresponding journal
/// rows. Pin both the deductible + non-deductible cases at the
/// US4 contract level: same 2-line journal regardless of the
/// FR-014 flag (the deductible bit drives FR-021 reporting,
/// not the books).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ExpensePostingJournalTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostedExpense_Emits_2LineBalancedJournal_RegardlessOfDeductibleFlag(
        bool deductible
    )
    {
        await using var db = await _fixture.CreateContextAsync();
        var (category, user) = await SeedAsync(db);

        var draft = Expense.CreateDraft(
            new DateOnly(2026, 5, 9),
            category.Id,
            MoneyEgp.From(250m),
            deductibleFlag: deductible,
            description: new ArabicEnglishText("اختبار", "Office supplies")
        );
        db.Add(draft);

        // FR-016 — deductible expense requires an attachment.
        if (deductible)
        {
            db.Add(
                new EgyptTax.Domain.Documents.Attachment(
                    documentId: draft.Id,
                    documentType: EgyptTax.Domain.Workflow.DocumentType.Expense,
                    filenameOriginal: "receipt.pdf",
                    filenameStorage: $"{Guid.NewGuid():N}.pdf",
                    relativePath: $"attachments/2026/05/{draft.Id:D}/receipt.pdf",
                    sha256: new byte[32],
                    mimeType: "application/pdf",
                    sizeBytes: 256,
                    uploadedByUserId: user.Id,
                    uploadedAtUtc: new DateTime(2026, 5, 9, 9, 0, 0, DateTimeKind.Utc)
                )
            );
        }
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var emitter = new ExpenseJournalEmitter(db);
        var handler = new PostExpenseHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore(),
            emitter
        );
        var posted = await handler.HandleAsync(
            new PostExpenseCommand(draft.Id, user.Id),
            CancellationToken.None
        );

        db.ChangeTracker.Clear();
        var je = await db.Set<EgyptTax.Domain.Accounting.JournalEntry>()
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);

        je.Lines.Should()
            .HaveCount(
                2,
                because: "expense JE is the simplest case — DR Expense / CR AP — irrespective of the FR-014 deductible flag"
            );
        je.Lines.Sum(l => l.Debit.Amount).Should().Be(250m);
        je.Lines.Sum(l => l.Credit.Amount).Should().Be(250m);

        var expense = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.GenericExpense);
        var ap = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsPayable);
        expense.Debit.Amount.Should().Be(250m);
        expense.Credit.Amount.Should().Be(0m);
        ap.Debit.Amount.Should().Be(0m);
        ap.Credit.Amount.Should().Be(250m);
    }

    private static async Task<(DeductibleExpenseCategory, User)> SeedAsync(AppDbContext db)
    {
        var category = new DeductibleExpenseCategory(
            code: $"EC-{Guid.NewGuid():N}".Substring(0, 8),
            name: new ArabicEnglishText("فئة", "Category"),
            defaultDeductible: false,
            defaultAccountId: Guid.NewGuid()
        );
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(category);
        db.Add(user);
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
