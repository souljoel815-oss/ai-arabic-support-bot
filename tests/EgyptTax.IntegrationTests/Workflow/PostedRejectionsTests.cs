using EgyptTax.Application.Audit;
using EgyptTax.Application.Expenses;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Purchases;
using EgyptTax.Application.Workflow;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Workflow;

/// <summary>
/// T153 / FR-012 / FR-027 / US3 scenario 3 — cross-type rejection
/// contract for posted documents AND T159 / FR-026 — early
/// approval-required guard in the post handlers.
///
/// PostedImmutabilityTests already exercises the SalesInvoice
/// surface; this file adds the parity coverage for PurchaseInvoice
/// + Expense (so an inspector can't sneak a mutation through the
/// less-tested doc types) and pins the FR-012 hint distinction:
/// tax-impacting docs route through credit notes (FR-013); non-
/// tax-impacting docs (JournalVoucher / payment vouchers) route
/// through reversal vouchers.
///
/// T159 is the matching FR-026 guarantee on the entry side: when
/// a doc type is configured as approval-required, attempting to
/// direct-post a Draft is rejected BEFORE the document number is
/// allocated — no wasted numbering slot, clear error message
/// pointing the operator at Submit + Approve.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PostedRejectionsTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    // ─────────────────────────────────────────────────────────────────
    // T153 — cross-type FR-012 rejection contract
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Posted_PurchaseInvoice_GuardRejects_Edit_Delete_Void_WithCreditNoteHint()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostPurchaseAsync(db);

        foreach (var op in new[] { "edit", "delete", "void" })
        {
            var act = () =>
                PostedDocumentImmutabilityGuard.EnsureNotPosted(
                    posted.State,
                    DocumentType.PurchaseInvoice,
                    posted.Id,
                    op
                );

            var ex = act.Should().Throw<InvalidOperationException>().Which;
            ex.Message.Should()
                .Contain(
                    "credit note (FR-013)",
                    because: $"PurchaseInvoice is tax-impacting (FR-012); the {op} rejection MUST surface the credit-note correction path"
                );
            ex.Message.Should()
                .Contain(op, because: "the rejection message MUST name the rejected operation");
            ex.Message.Should()
                .Contain(
                    posted.Id.ToString("D"),
                    because: "the rejection message MUST identify the document"
                );
        }
    }

    [Fact]
    public async Task Posted_Expense_GuardRejects_Edit_Delete_Void_WithCreditNoteHint()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostExpenseAsync(db);

        foreach (var op in new[] { "edit", "delete", "void" })
        {
            var act = () =>
                PostedDocumentImmutabilityGuard.EnsureNotPosted(
                    posted.State,
                    DocumentType.Expense,
                    posted.Id,
                    op
                );

            var ex = act.Should().Throw<InvalidOperationException>().Which;
            ex.Message.Should()
                .Contain(
                    "credit note (FR-013)",
                    because: $"Expense is tax-impacting (FR-012); the {op} rejection MUST surface the credit-note correction path"
                );
            ex.Message.Should().Contain(op);
            ex.Message.Should().Contain(posted.Id.ToString("D"));
        }
    }

    [Fact]
    public async Task Posted_PurchaseInvoice_AddLine_Throws_AtEntityBoundary()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostPurchaseAsync(db);

        var act = () =>
            posted.AddLine(
                itemId: null,
                expenseCategoryId: Guid.NewGuid(),
                quantity: 1m,
                unitPrice: MoneyEgp.From(50m),
                vatCategoryId: Guid.NewGuid(),
                vatRatePercent: 14m,
                deductibleFlag: false
            );

        act.Should()
            .Throw<InvalidOperationException>(
                because: "the PurchaseInvoice entity invariant rejects mutation of any non-Draft state — the application-level guard is defence-in-depth, the entity is the bedrock"
            );
    }

    [Fact]
    public async Task Posted_Expense_UpdateAmount_Throws_AtEntityBoundary()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostExpenseAsync(db);

        var act = () => posted.UpdateAmount(MoneyEgp.From(999m));

        act.Should()
            .Throw<InvalidOperationException>(
                because: "the Expense entity invariant rejects mutation of any non-Draft state"
            );
    }

    [Theory]
    [InlineData(DocumentType.SalesInvoice)]
    [InlineData(DocumentType.CreditNote)]
    [InlineData(DocumentType.PurchaseInvoice)]
    [InlineData(DocumentType.Expense)]
    [InlineData(DocumentType.FixedAsset)]
    public void TaxImpactingType_RejectionHint_PointsToCreditNote(DocumentType type)
    {
        var act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                DocumentState.Posted,
                type,
                Guid.NewGuid(),
                "edit"
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*credit note (FR-013)*",
                because: $"{type} is tax-impacting per FR-012 — corrections route through credit notes"
            );
    }

    [Theory]
    [InlineData(DocumentType.JournalVoucher)]
    [InlineData(DocumentType.SupplierPaymentVoucher)]
    [InlineData(DocumentType.CustomerReceiptVoucher)]
    public void NonTaxImpactingType_RejectionHint_PointsToReversalVoucher(DocumentType type)
    {
        var act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                DocumentState.Posted,
                type,
                Guid.NewGuid(),
                "edit"
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*reversal voucher*",
                because: $"{type} is non-tax-impacting per FR-012 — corrections route through reversal vouchers, NOT credit notes"
            );
    }

    // ─────────────────────────────────────────────────────────────────
    // T159 — FR-026 early approval-required guard in post handlers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostSalesInvoice_RefusesDirectPost_When_ApprovalRequired_AndDocIsDraft()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SetApprovalRequiredAsync(db, DocumentType.SalesInvoice, required: true);
        var (customer, _, vat, user) = await SeedSalesMastersAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(
            itemId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(100m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent
        );
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));
        var allocator = new SqlSequentialNumberAllocator(db);
        var audit = new CaptureAuditLogStore();
        var handler = new PostSalesInvoiceHandler(db, allocator, clock, audit);

        var act = async () =>
            await handler.HandleAsync(
                new PostSalesInvoiceCommand(draft.Id, user.Id),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(ex =>
                ex.Message.Contains("FR-026", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("approval", StringComparison.OrdinalIgnoreCase)
            );

        // Critical: the document MUST still be Draft + un-numbered.
        // If the allocator ran before the guard fired, the test catches
        // the regression by finding a non-null DocumentNumber (a wasted
        // numbering slot that survives the rolled-back save).
        db.ChangeTracker.Clear();
        var refreshed = await db.Set<SalesInvoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == draft.Id);
        refreshed.State.Should().Be(DocumentState.Draft);
        refreshed
            .DocumentNumber.Should()
            .BeNull(
                because: "T159 — the early guard MUST fire BEFORE the document-number allocator runs, otherwise rejected posts leak numbering slots"
            );
    }

    [Fact]
    public async Task PostPurchaseInvoice_RefusesDirectPost_When_ApprovalRequired_AndDocIsDraft()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SetApprovalRequiredAsync(db, DocumentType.PurchaseInvoice, required: true);
        var (supplier, vat, user) = await SeedPurchaseMastersAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-T159",
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
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        );

        var act = async () =>
            await handler.HandleAsync(
                new PostPurchaseInvoiceCommand(draft.Id, user.Id),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-026", StringComparison.OrdinalIgnoreCase));

        db.ChangeTracker.Clear();
        var refreshed = await db.Set<PurchaseInvoice>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == draft.Id);
        refreshed.State.Should().Be(DocumentState.Draft);
        refreshed.DocumentNumber.Should().BeNull();
    }

    [Fact]
    public async Task PostExpense_RefusesDirectPost_When_ApprovalRequired_AndDocIsDraft()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SetApprovalRequiredAsync(db, DocumentType.Expense, required: true);
        var (category, user) = await SeedExpenseMastersAsync(db);

        var draft = Expense.CreateDraft(
            new DateOnly(2026, 5, 7),
            category.Id,
            MoneyEgp.From(200m),
            deductibleFlag: false,
            description: new ArabicEnglishText("ضيافة", "Entertainment")
        );
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = new PostExpenseHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        );

        var act = async () =>
            await handler.HandleAsync(
                new PostExpenseCommand(draft.Id, user.Id),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-026", StringComparison.OrdinalIgnoreCase));

        db.ChangeTracker.Clear();
        var refreshed = await db.Set<Expense>().AsNoTracking().FirstAsync(e => e.Id == draft.Id);
        refreshed.State.Should().Be(DocumentState.Draft);
        refreshed.DocumentNumber.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────
    // Seed + helper plumbing
    // ─────────────────────────────────────────────────────────────────

    private static async Task SetApprovalRequiredAsync(
        AppDbContext db,
        DocumentType documentType,
        bool required
    )
    {
        var setting = await db.Set<DocumentTypeApprovalSetting>()
            .FirstAsync(s => s.DocumentType == documentType);
        setting.SetApprovalRequired(required);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task<PurchaseInvoice> PostPurchaseAsync(AppDbContext db)
    {
        var (supplier, vat, user) = await SeedPurchaseMastersAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-T153",
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
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        );
        return await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );
    }

    private static async Task<Expense> PostExpenseAsync(AppDbContext db)
    {
        var (category, user) = await SeedExpenseMastersAsync(db);

        var draft = Expense.CreateDraft(
            new DateOnly(2026, 5, 7),
            category.Id,
            MoneyEgp.From(200m),
            deductibleFlag: false,
            description: new ArabicEnglishText("ضيافة", "Entertainment")
        );
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = new PostExpenseHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        );
        return await handler.HandleAsync(
            new PostExpenseCommand(draft.Id, user.Id),
            CancellationToken.None
        );
    }

    private static async Task<(
        Customer Customer,
        Item Item,
        VatCategory Vat,
        User User
    )> SeedSalesMastersAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        var customer = new Customer(
            code: $"CUS-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo",
                "Downtown",
                "Tahrir",
                "1"
            ),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                vatExemption: false,
                defaultSalesVatCategoryId: vat.Id
            )
        );
        var item = new Item(
            code: $"IT-{Guid.NewGuid():N}".Substring(0, 8),
            name: new ArabicEnglishText("بند", "Item"),
            defaultVatCategoryId: vat.Id
        );
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(vat);
        db.Add(customer);
        db.Add(item);
        db.Add(user);
        await db.SaveChangesAsync();
        return (customer, item, vat, user);
    }

    private static async Task<(
        Supplier Supplier,
        VatCategory Vat,
        User User
    )> SeedPurchaseMastersAsync(AppDbContext db)
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

    private static async Task<(
        DeductibleExpenseCategory Category,
        User User
    )> SeedExpenseMastersAsync(AppDbContext db)
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
