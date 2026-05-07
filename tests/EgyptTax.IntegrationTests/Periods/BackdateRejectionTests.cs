using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Periods;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Periods;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Periods;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Periods;

/// <summary>
/// T246a / SC-004 / FR-037 — full backdate-rejection flow against
/// real Testcontainers SQL: close the May VAT period, attempt to
/// post a sales invoice dated inside May, verify the post is
/// rejected with a clear FR-037 error AND the rejection is
/// recorded in the audit log; an Administrator reopens the
/// period; the same post then succeeds; audit log shows close +
/// rejected-attempt + reopen + post in chronological order.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BackdateRejectionTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Lock_Then_Backdate_Then_Reopen_Then_Post_Audits_Every_Step()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, admin) = await SeedAsync(db);
        var captureAudit = new CaptureAuditLogStore();

        // Step 1: Administrator locks the May 2026 VAT period.
        var lockClock = new TestClock(new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));
        var lockHandler = new LockTaxPeriodHandler(db, lockClock, captureAudit);
        await lockHandler.HandleAsync(new LockTaxPeriodCommand(
            TaxPeriodKind.VatMonth, 2026, 5, admin.Id, "VAT return filed for May 2026"),
            CancellationToken.None);

        captureAudit.Captured.Should().ContainSingle(e => e.Kind == "tax_period.locked");

        // Step 2: a Bookkeeper drafts an invoice dated May 28 (inside
        // the locked period) and tries to post it.
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 28));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var postClock = new TestClock(new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc));
        var postHandler = new PostSalesInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), postClock, captureAudit,
            journalEmitter: null,
            periodLockGuard: new SqlTaxPeriodLockGuard(db));

        var rejectAct = async () => await postHandler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, admin.Id), CancellationToken.None);
        await rejectAct.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-037", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("Locked", StringComparison.OrdinalIgnoreCase));

        captureAudit.Captured.Should().ContainSingle(e => e.Kind == "tax_period.post_rejected",
            because: "the rejected backdate attempt MUST be audit-logged per SC-004 — a silent rejection would leave no forensic trace");

        var stillDraft = await db.Set<SalesInvoice>().AsNoTracking().FirstAsync(i => i.Id == draft.Id);
        stillDraft.State.Should().Be(DocumentState.Draft);
        stillDraft.DocumentNumber.Should().BeNull(
            because: "the FR-011 sequential number was NOT burned — operator can fix and re-attempt without a numbering gap");

        // Step 3: Administrator reopens the period.
        var reopenClock = new TestClock(new DateTime(2026, 6, 2, 14, 0, 0, DateTimeKind.Utc));
        var reopenHandler = new LockTaxPeriodHandler(db, reopenClock, captureAudit);
        await reopenHandler.ReopenAsync(new ReopenTaxPeriodCommand(
            TaxPeriodKind.VatMonth, 2026, 5, admin.Id, "Late supplier invoice received — reopen for one post"),
            CancellationToken.None);

        captureAudit.Captured.Should().ContainSingle(e => e.Kind == "tax_period.reopened");

        // Step 4: the post now succeeds against the reopened period.
        var postAfterReopen = new PostSalesInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), reopenClock, captureAudit,
            journalEmitter: null,
            periodLockGuard: new SqlTaxPeriodLockGuard(db));
        var posted = await postAfterReopen.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, admin.Id), CancellationToken.None);

        posted.State.Should().Be(DocumentState.Posted);
        posted.DocumentNumber.Should().NotBeNullOrWhiteSpace();

        // Final audit-chain shape: close → rejected-attempt → reopen → post (in order).
        var keys = captureAudit.Captured.Select(e => e.Kind).ToList();
        keys.Should().ContainInOrder(ExpectedAuditChainShape,
            because: "the audit chain MUST tell the full story in order so an inspector can reconstruct what the operator did and when");
    }

    private static readonly string[] ExpectedAuditChainShape =
    {
        "tax_period.locked",
        "tax_period.post_rejected",
        "tax_period.reopened",
        "sales_invoice.posted",
    };

    [Fact]
    public async Task Post_OutsideLockedPeriod_Is_Allowed_EvenWhenAdjacent()
    {
        // Boundary check: lock May; post dated June 1 should succeed
        // — the guard is per-month, not "from any locked month onwards".
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, admin) = await SeedAsync(db);
        var captureAudit = new CaptureAuditLogStore();

        var lockClock = new TestClock(new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));
        await new LockTaxPeriodHandler(db, lockClock, captureAudit)
            .HandleAsync(new LockTaxPeriodCommand(
                TaxPeriodKind.VatMonth, 2026, 5, admin.Id, "filed"),
                CancellationToken.None);

        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 6, 1));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(500m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var postHandler = new PostSalesInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            captureAudit,
            journalEmitter: null,
            periodLockGuard: new SqlTaxPeriodLockGuard(db));
        var posted = await postHandler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, admin.Id), CancellationToken.None);

        posted.State.Should().Be(DocumentState.Posted,
            because: "June posts are unaffected by a May lock — the per-month guard does not bleed forward");
    }

    private static async Task<(Customer, Item, VatCategory, User)> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var customer = new Customer(
            code: $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "1"),
            taxProfile: CustomerTaxProfile.B2BRegistered(EgyptianTin.Parse("987654321"), false, vat.Id));
        var item = new Item(
            code: $"ITEM-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("صنف", "Item"),
            defaultVatCategoryId: vat.Id);
        var admin = new User(
            email: $"admin-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مدير", "Admin"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(customer); db.Add(item); db.Add(admin);
        await db.SaveChangesAsync();
        return (customer, item, vat, admin);
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
