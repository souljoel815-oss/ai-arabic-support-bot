using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Application.Expenses;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Periods;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Compliance;
using EgyptTax.Infrastructure.Eta;
using EgyptTax.Infrastructure.Expenses;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Periods;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Compliance;

/// <summary>
/// T235 / Differentiator 2 — verifies the cockpit aggregates the
/// signals it advertises into a coherent view-model. The cockpit
/// itself does not introduce new domain logic; this test makes sure
/// the projection wires the right rows together when a period has
/// the canonical mix of clean + dirty + draft documents.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ClosingCockpitAggregationTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Cockpit_Aggregates_VatReadiness_Drafts_FailedEta_MissingAttachments_PeriodLockChecklist()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, supplier, item, vat, category, user) = await SeedAsync(db);

        // Posted clean sales invoice (with auto-Submitted ETA via 0% mock).
        await PostSalesAsync(
            db,
            customer,
            item,
            vat,
            user,
            new DateOnly(2026, 5, 5),
            1_000m,
            etaFailureRate: 0.0
        );

        // Posted sales invoice with FAILED ETA submission (100% mock).
        await PostSalesAsync(
            db,
            customer,
            item,
            vat,
            user,
            new DateOnly(2026, 5, 8),
            2_000m,
            etaFailureRate: 1.0
        );

        // Posted purchase with deductible line + attachment (clean).
        await PostPurchaseAsync(
            db,
            supplier,
            vat,
            user,
            new DateOnly(2026, 5, 10),
            800m,
            deductible: true,
            attachments: 1,
            "SUP-001"
        );

        // Posted EXPENSE marked deductible WITHOUT attachment.
        // FR-016 normally rejects this at post-time, but the post
        // handler in this batch is constructed without the
        // periodLockGuard so we hand-craft the post via raw EF +
        // MarkPosted to seed the "clean post but missing attachment
        // appears in cockpit anyway" case.
        await PostExpenseDirectlyAsync(
            db,
            category,
            user,
            new DateOnly(2026, 5, 12),
            300m,
            deductible: true,
            attachmentCount: 0
        );

        // Draft sales invoice in period (must clear before lock).
        var draftSales = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 20)
        );
        draftSales.AddLine(item.Id, 1m, MoneyEgp.From(150m), vat.Id, vat.RatePercent);
        db.Add(draftSales);
        await db.SaveChangesAsync();

        // Run cockpit.
        var cockpit = await new SqlMonthlyTaxClosingCockpitQuery(db).RunAsync(2026, 5);

        cockpit.PeriodIsLocked.Should().BeFalse();
        cockpit
            .TotalPostsInPeriod.Should()
            .BeGreaterThanOrEqualTo(
                4,
                because: "2 sales + 1 purchase + 1 expense = 4 posted documents minimum"
            );
        cockpit
            .DraftsInPeriodCount.Should()
            .BeGreaterThanOrEqualTo(
                1,
                because: "the May draft sales invoice MUST surface in the drafts count"
            );

        cockpit
            .FailedEtaSubmissionCount.Should()
            .BeGreaterThanOrEqualTo(
                1,
                because: "one sales invoice was posted with the 100%-failure mock submitter"
            );

        cockpit
            .MissingDocuments.Should()
            .Contain(
                b =>
                    b.Name.Contains(
                        "deductible expenses with no attachment",
                        StringComparison.OrdinalIgnoreCase
                    ),
                because: "the deductible-expense-with-no-attachment bucket MUST be surfaced"
            );

        cockpit
            .VatReadinessPercent.Should()
            .BeLessThan(
                100m,
                because: "with at least one failed ETA + one missing-attachment expense, readiness cannot be 100%"
            );

        // Period-lock checklist: drafts-cleared MUST be false; lock-not-yet
        // MUST be true (open period).
        var draftsItem = cockpit.PeriodLockChecklist.Single(c =>
            c.Description.Contains("drafts dated in period", StringComparison.OrdinalIgnoreCase)
        );
        draftsItem.Cleared.Should().BeFalse();
        draftsItem.RelatedCount.Should().BeGreaterThanOrEqualTo(1);

        var notLockedItem = cockpit.PeriodLockChecklist.Single(c =>
            c.Description.Contains("not yet locked", StringComparison.OrdinalIgnoreCase)
        );
        notLockedItem.Cleared.Should().BeTrue();
    }

    [Fact]
    public async Task Cockpit_Reflects_LockedPeriod_State_FromTaxPeriodTable()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (_, _, _, _, _, admin) = await SeedAsync(db);

        // Lock the month.
        var lockClock = new TestClock(new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc));
        await new LockTaxPeriodHandler(db, lockClock, new CaptureAuditLogStore()).HandleAsync(
            new LockTaxPeriodCommand(
                EgyptTax.Domain.Periods.TaxPeriodKind.VatMonth,
                2026,
                6,
                admin.Id,
                "filed"
            ),
            CancellationToken.None
        );

        var cockpit = await new SqlMonthlyTaxClosingCockpitQuery(db).RunAsync(2026, 6);

        cockpit.PeriodIsLocked.Should().BeTrue();
        cockpit
            .PeriodLockChecklist.Single(c =>
                c.Description.Contains("not yet locked", StringComparison.OrdinalIgnoreCase)
            )
            .Cleared.Should()
            .BeFalse(
                because: "once the period is Locked the 'not yet locked' check naturally flips to NOT cleared"
            );
    }

    [Fact]
    public async Task EmptyPeriod_Yields_100Percent_Readiness_AndCleanChecklist()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedAsync(db);

        // Query a month with NO documents.
        var cockpit = await new SqlMonthlyTaxClosingCockpitQuery(db).RunAsync(2026, 8);

        cockpit.TotalPostsInPeriod.Should().Be(0);
        cockpit
            .VatReadinessPercent.Should()
            .Be(
                100m,
                because: "no documents = no risks = 100% ready by definition (the 0/0 division is hard-coded to 100)"
            );
        cockpit.DraftsInPeriodCount.Should().Be(0);
        cockpit.MissingDocuments.Should().BeEmpty();
        cockpit.FailedEtaSubmissionCount.Should().Be(0);
    }

    private static async Task PostSalesAsync(
        AppDbContext db,
        Customer customer,
        Item item,
        VatCategory vat,
        User user,
        DateOnly date,
        decimal unitPrice,
        double etaFailureRate
    )
    {
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, date);
        draft.AddLine(item.Id, 1m, MoneyEgp.From(unitPrice), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
        var inner = new PostSalesInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore()
        );
        await EnsureCompanyAsync(db);
        var wrapper = new PostSalesInvoiceWithEtaSubmissionHandler(
            inner,
            db,
            new MockEtaSubmitter(etaFailureRate),
            new EInvoiceJsonGenerator(),
            new CaptureAuditLogStore(),
            clock
        );
        await wrapper.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );
    }

    private static async Task PostPurchaseAsync(
        AppDbContext db,
        Supplier supplier,
        VatCategory vat,
        User user,
        DateOnly date,
        decimal unitPrice,
        bool deductible,
        int attachments,
        string supplierInvoiceNumber
    )
    {
        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            supplierInvoiceNumber,
            date
        );
        draft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(unitPrice),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent,
            deductibleFlag: deductible
        );
        db.Add(draft);
        for (var i = 0; i < attachments; i++)
        {
            db.Add(
                new Attachment(
                    draft.Id,
                    DocumentType.PurchaseInvoice,
                    $"r-{i}.pdf",
                    $"{Guid.NewGuid():N}.pdf",
                    $"attachments/2026/{date.Month:D2}/{draft.Id:D}/r-{i}.pdf",
                    new byte[32],
                    "application/pdf",
                    1,
                    user.Id,
                    date.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime()
                )
            );
        }
        await db.SaveChangesAsync();

        var clock = new TestClock(date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
        var handler = new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore()
        );
        await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );
    }

    /// <summary>
    /// Bypasses PostExpenseHandler's FR-016 enforcement so we can
    /// seed the "deductible expense WITHOUT attachment" case the
    /// cockpit's missing-document bucket is supposed to surface.
    /// In production this state shouldn't exist; the cockpit guards
    /// against historical data that pre-dates the FR-016 enforcement.
    /// </summary>
    private static async Task PostExpenseDirectlyAsync(
        AppDbContext db,
        DeductibleExpenseCategory category,
        User user,
        DateOnly date,
        decimal amount,
        bool deductible,
        int attachmentCount
    )
    {
        var expense = Expense.CreateDraft(
            date,
            category.Id,
            MoneyEgp.From(amount),
            deductible,
            new ArabicEnglishText("وصف", "Test expense")
        );
        expense.MarkPosted(
            documentNumber: $"EXP-2026-{Guid.NewGuid().GetHashCode():X8}",
            postedByUserId: user.Id,
            postedAtUtc: date.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime(),
            postingMode: DocumentPostingMode.UnapprovedDirect,
            approvalEnabled: false
        );
        db.Add(expense);
        for (var i = 0; i < attachmentCount; i++)
        {
            db.Add(
                new Attachment(
                    expense.Id,
                    DocumentType.Expense,
                    $"r-{i}.pdf",
                    $"{Guid.NewGuid():N}.pdf",
                    $"attachments/2026/{date.Month:D2}/{expense.Id:D}/r-{i}.pdf",
                    new byte[32],
                    "application/pdf",
                    1,
                    user.Id,
                    date.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime()
                )
            );
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync())
            return;
        db.Add(
            new Company(
                legalName: new ArabicEnglishText("شركة", "Test Company"),
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

    private static async Task<(
        Customer customer,
        Supplier supplier,
        Item item,
        VatCategory vat,
        DeductibleExpenseCategory category,
        User user
    )> SeedAsync(AppDbContext db)
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
            code: $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
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
                false,
                vat.Id
            )
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
        var item = new Item(
            code: $"ITEM-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("صنف", "Item"),
            defaultVatCategoryId: vat.Id
        );
        var category = new DeductibleExpenseCategory(
            code: $"CAT-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("فئة", "Category"),
            defaultDeductible: true,
            defaultAccountId: Guid.NewGuid()
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
        db.Add(supplier);
        db.Add(item);
        db.Add(category);
        db.Add(user);
        await db.SaveChangesAsync();
        return (customer, supplier, item, vat, category, user);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public Task<AuditLogEntry> AppendAsync(
            AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            return Task.FromResult(
                new AuditLogEntry(
                    index: 1,
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
