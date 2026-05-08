using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Eta;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Eta;

/// <summary>
/// T094 / T095 — auto-submission orchestration. The wrapper handler
/// (`PostSalesInvoiceWithEtaSubmissionHandler`) MUST: (a) post the
/// invoice via the bare `PostSalesInvoiceHandler` so the existing
/// post-time invariants hold, (b) hydrate the eInvoice JSON via
/// `EInvoiceJsonGenerator`, (c) call `IEtaSubmitter` with that JSON,
/// (d) record the outcome via `EtaSubmission.RecordAttempt`, and
/// (e) emit an FR-028 audit event keyed `eta_submission.submitted`
/// or `eta_submission.failed`. The simulated-failure path is
/// exercised so the dashboard's Failed branch is observably correct,
/// not just structurally present.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AutoSubmissionTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Post_WithMockSucceeding_FlipsEtaSubmissionTo_Submitted()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (invoice, audit) = await PostInvoiceAsync(db, failureRate: 0.0);

        var etaRow = await db.Set<EtaSubmission>()
            .AsNoTracking()
            .FirstAsync(s => s.SalesInvoiceId == invoice.Id);
        etaRow
            .Status.Should()
            .Be(
                EtaSubmissionStatus.Submitted,
                because: "0% failure rate — every attempt MUST succeed"
            );
        etaRow
            .SubmissionUuid.Should()
            .NotBeNullOrWhiteSpace(because: "the mock always returns a synthetic UUID on success");
        etaRow.AttemptCount.Should().Be(1);
        etaRow.LastAttemptAtUtc.Should().NotBeNull();
        etaRow.ErrorCode.Should().BeNull();

        audit
            .Captured.Should()
            .ContainSingle(
                e => e.Kind == "eta_submission.submitted",
                because: "every successful submit MUST emit one FR-028 audit event keyed eta_submission.submitted"
            );
        audit
            .Captured.Should()
            .Contain(
                e => e.Kind == "sales_invoice.posted",
                because: "the bare post audit event also fires through the inner handler"
            );
    }

    [Fact]
    public async Task Post_WithMockAlwaysFailing_FlipsEtaSubmissionTo_Failed_AndRecordsErrorCode()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (invoice, audit) = await PostInvoiceAsync(db, failureRate: 1.0);

        var etaRow = await db.Set<EtaSubmission>()
            .AsNoTracking()
            .FirstAsync(s => s.SalesInvoiceId == invoice.Id);
        etaRow
            .Status.Should()
            .Be(
                EtaSubmissionStatus.Failed,
                because: "100% failure rate — every attempt MUST be reported as Failed"
            );
        etaRow.SubmissionUuid.Should().BeNull();
        etaRow.AttemptCount.Should().Be(1);
        etaRow.ErrorCode.Should().Be("ETA_MOCK_500");
        etaRow.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        audit
            .Captured.Should()
            .ContainSingle(
                e => e.Kind == "eta_submission.failed",
                because: "every failed submit MUST emit one FR-028 audit event keyed eta_submission.failed — never silently fails per spec edge case 'Mock ETA submission failure'"
            );

        // The invoice itself remains Posted regardless of ETA outcome —
        // the document is committed; the ETA round-trip is a downstream
        // concern that must not roll back the post.
        invoice.State.Should().Be(EgyptTax.Domain.Workflow.DocumentState.Posted);
    }

    [Fact]
    public async Task Post_AuditEventForSubmission_ContainsOutcomeAndDocumentNumber()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (invoice, audit) = await PostInvoiceAsync(db, failureRate: 0.0);

        var entry = audit.Captured.Single(e => e.Kind == "eta_submission.submitted");
        entry
            .PayloadJson.Should()
            .Contain(
                "Submitted",
                because: "the outcome string lives in the audit payload for FR-028 chain integrity"
            );
        entry
            .PayloadJson.Should()
            .Contain(
                invoice.DocumentNumber!,
                because: "an inspector reading the audit row MUST be able to identify the document without joining"
            );
        entry.PayloadJson.Should().Contain("attempt_count");
    }

    private static async Task<(SalesInvoice invoice, CaptureAuditLogStore audit)> PostInvoiceAsync(
        AppDbContext db,
        double failureRate
    )
    {
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        await SeedCompanyAsync(db);
        var operatorUser = await SeedOperatorUserAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var allocator = new SqlSequentialNumberAllocator(db);
        var auditCapture = new CaptureAuditLogStore();

        var innerHandler = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture);
        var submitter = new MockEtaSubmitter(failureRate);
        var jsonGenerator = new EInvoiceJsonGenerator();
        var wrapper = new PostSalesInvoiceWithEtaSubmissionHandler(
            innerHandler,
            db,
            submitter,
            jsonGenerator,
            auditCapture,
            clock
        );

        var posted = await wrapper.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );
        return (posted, auditCapture);
    }

    private static async Task<(Customer customer, Item item, VatCategory vat)> SeedMasterDataAsync(
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
        var customer = new Customer(
            code: "CUST-001",
            name: new ArabicEnglishText("عميل تجريبي", "Test Customer LLC"),
            address: PostalAddress.Create(
                display: new ArabicEnglishText("القاهرة", "Cairo"),
                governorate: "Cairo",
                regionCity: "Downtown",
                street: "Tahrir",
                buildingNumber: "1"
            ),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                tin: EgyptianTin.Parse("987654321"),
                vatExemption: false,
                defaultSalesVatCategoryId: vat.Id
            )
        );
        var item = new Item(
            code: "ITEM-001",
            name: new ArabicEnglishText("ساعة استشارة", "Consulting Hour"),
            defaultVatCategoryId: vat.Id
        );
        db.Add(vat);
        db.Add(customer);
        db.Add(item);
        await db.SaveChangesAsync();
        return (customer, item, vat);
    }

    private static async Task SeedCompanyAsync(AppDbContext db)
    {
        var existing = await db.Set<Company>().FirstOrDefaultAsync();
        if (existing is not null)
            return;
        var company = new Company(
            legalName: new ArabicEnglishText("شركة الاختبار", "Test Company SAE"),
            taxRegistrationNumber: EgyptianTin.Parse("123456789"),
            commercialRegistrationNumber: "CR-001234",
            address: PostalAddress.Create(
                display: new ArabicEnglishText("القاهرة", "Cairo"),
                governorate: "Cairo",
                regionCity: "Downtown",
                street: "Tahrir",
                buildingNumber: "12",
                postalCode: "11511"
            ),
            taxpayerActivityCode: "0001"
        );
        db.Add(company);
        await db.SaveChangesAsync();
    }

    private static async Task<User> SeedOperatorUserAsync(AppDbContext db)
    {
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Operator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(user);
        await db.SaveChangesAsync();
        return user;
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
            var entry = new AuditLogEntry(
                index: Captured.Count,
                tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId,
                actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId,
                kind: payload.Kind,
                payloadJson: payload.PayloadJson,
                prevHash: new byte[32],
                thisHash: new byte[32]
            );
            return Task.FromResult(entry);
        }
    }
}
