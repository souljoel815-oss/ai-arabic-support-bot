using EgyptTax.Application.Audit;
using EgyptTax.Application.Expenses;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Configuration;

/// <summary>
/// T174 / US5 scenario 1 / FR-015 — a deductible-expense-category
/// change MUST NOT retroactively affect pre-existing posted
/// expenses. The Expense aggregate stores its own
/// <c>DeductibleFlag</c> at create-time + post-time; the category's
/// <c>DefaultDeductible</c> is only a default for new rows. After
/// the operator flips the category default, posted expenses keep
/// their original flag — historical accounting decisions stay
/// stable.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class CategoryRetroactivityTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task UpdateCategoryDefaults_DoesNotMutate_PostedExpenseFlag()
    {
        await using var db = await _fixture.CreateContextAsync();

        var category = new DeductibleExpenseCategory(
            code: $"CAT-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("فئة", "Category"),
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

        // Post a non-deductible expense referencing this category
        // (we explicitly pick the non-default flag at create time
        // — the operator chose to mark it non-deductible despite
        // the category's default). Non-deductible to skip the
        // FR-016 attachment requirement.
        var draft = Expense.CreateDraft(
            documentDate: new DateOnly(2026, 5, 7),
            categoryId: category.Id,
            amount: MoneyEgp.From(500m),
            deductibleFlag: false,
            description: new ArabicEnglishText("اختبار", "Test"));
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var posted = await new PostExpenseHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore(),
                new ExpenseJournalEmitter(db))
            .HandleAsync(new PostExpenseCommand(draft.Id, user.Id), CancellationToken.None);

        posted.DeductibleFlag.Should().BeFalse();
        var originalAmount = posted.Amount.Amount;

        // Now the operator updates the category's defaults — flips
        // defaultDeductible from true → false + changes the
        // account.
        var newAccountId = Guid.NewGuid();
        category.UpdateDefaults(deductible: false, accountId: newAccountId);
        await db.SaveChangesAsync();

        // Reload the posted expense — its DeductibleFlag MUST not
        // have changed; its Amount MUST not have changed.
        db.ChangeTracker.Clear();
        var refreshed = await db.Set<Expense>().AsNoTracking()
            .FirstAsync(e => e.Id == posted.Id);

        refreshed.DeductibleFlag.Should().Be(false,
            because: "FR-015 / US5 scenario 1 — category-default updates MUST NOT retroactively flip posted-expense flags");
        refreshed.Amount.Amount.Should().Be(originalAmount,
            because: "Amount + other fields are aggregate state, not derived from the category — they stay frozen");
        refreshed.CategoryId.Should().Be(category.Id,
            because: "the FK back-pointer stays — only the per-row data on the parent category changed");
        refreshed.State.Should().Be(DocumentState.Posted);
    }

    [Fact]
    public async Task DeactivateCategory_DoesNotMutate_PostedExpenseState()
    {
        // Edge case: operator deactivates the category entirely —
        // posted expenses MUST still be readable + their fields
        // unchanged. The deactivated state only affects whether
        // NEW expenses can pick the category at create time.
        await using var db = await _fixture.CreateContextAsync();

        var category = new DeductibleExpenseCategory(
            code: $"CAT-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("فئة", "Category"),
            defaultDeductible: false,
            defaultAccountId: Guid.NewGuid());
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(category); db.Add(user);
        await db.SaveChangesAsync();

        var draft = Expense.CreateDraft(
            documentDate: new DateOnly(2026, 5, 7),
            categoryId: category.Id,
            amount: MoneyEgp.From(750m),
            deductibleFlag: false,
            description: new ArabicEnglishText("اختبار", "Test"));
        db.Add(draft);
        await db.SaveChangesAsync();
        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var posted = await new PostExpenseHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore(),
                new ExpenseJournalEmitter(db))
            .HandleAsync(new PostExpenseCommand(draft.Id, user.Id), CancellationToken.None);

        // Deactivate the category.
        category.Deactivate();
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var refreshed = await db.Set<Expense>().AsNoTracking()
            .FirstAsync(e => e.Id == posted.Id);
        refreshed.Amount.Amount.Should().Be(750m);
        refreshed.State.Should().Be(DocumentState.Posted);
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
