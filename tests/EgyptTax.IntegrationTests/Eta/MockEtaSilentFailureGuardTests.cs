using EgyptTax.Application.Audit;
using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Application.Eta;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.BackgroundJobs;
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
/// T084a (Round-6 F11) — silent-failure guard for the spec edge case
/// "Mock ETA submission failure". When the regulator endpoint returns
/// a 5xx, the system MUST NOT swallow it: the invoice stays Posted,
/// the EtaSubmission row flips to Failed (queued for retry by the
/// recurring Hangfire job per T099), the document's Tax Risk Score
/// surfaces the failure to the operator, and the FR-028 audit log
/// records both the post and the failed submission. This single test
/// binds the full chain together so a regression in any link is
/// caught here even if the per-link tests (AutoSubmissionTests,
/// EtaSubmissionRetryJobTests, EtaSubmissionFailedRuleTests) still
/// pass individually.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MockEtaSilentFailureGuardTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task PostInvoice_When_MockEtaReturns500_NeverSilentlyFails()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        await SeedCompanyAsync(db);
        var operatorUser = await SeedOperatorUserAsync(db);

        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 7));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var nowUtc = new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(nowUtc);
        var allocator = new SqlSequentialNumberAllocator(db);
        var auditCapture = new CaptureAuditLogStore();
        var inner = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture);
        var failingSubmitter = new MockEtaSubmitter(failureRate: 1.0);
        var wrapper = new PostSalesInvoiceWithEtaSubmissionHandler(
            inner, db, failingSubmitter, new EInvoiceJsonGenerator(), auditCapture, clock);

        var posted = await wrapper.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id), CancellationToken.None);

        // 1 — invoice survives. The post commits regardless of the
        //     downstream ETA round-trip; rolling back here would lose
        //     work for the operator and create a "phantom invoice"
        //     state mismatch.
        posted.State.Should().Be(DocumentState.Posted,
            because: "the post itself MUST commit even when the regulator endpoint fails");
        posted.DocumentNumber.Should().NotBeNullOrWhiteSpace(
            because: "the FR-011 sequential number was already allocated and persisted");

        // 2 — ETA row reflects the failure verbatim and is retry-eligible.
        var etaRow = await db.Set<EtaSubmission>().AsNoTracking()
            .FirstAsync(s => s.SalesInvoiceId == posted.Id);
        etaRow.Status.Should().Be(EtaSubmissionStatus.Failed);
        etaRow.AttemptCount.Should().Be(1);
        etaRow.ErrorCode.Should().Be("ETA_MOCK_500");
        etaRow.SubmissionWindowExpiresAtUtc.Should().BeAfter(nowUtc,
            because: "the 7-day submission window is still open at t=post — the row is retry-eligible by the recurring job");

        // 3 — retry job picks it up on the next tick. Couples T099
        //     to T084a so the closed loop is observably correct.
        var retryJob = new EtaSubmissionRetryJob(db, failingSubmitter,
            new EInvoiceJsonGenerator(), auditCapture, clock);
        var retryResult = await retryJob.RunOnceAsync();
        retryResult.TotalCandidates.Should().BeGreaterThanOrEqualTo(1,
            because: "the retry job's candidate query MUST surface this row");
        var refreshed = await db.Set<EtaSubmission>().AsNoTracking()
            .FirstAsync(s => s.SalesInvoiceId == posted.Id);
        refreshed.AttemptCount.Should().Be(2,
            because: "the retry tick incremented the attempt counter — proof the row was actually picked up");

        // 4 — Tax Risk Score surfaces the failure to the user. This is
        //     the user-facing channel that closes the "silently fails"
        //     gap: an operator who never opens the dashboard still
        //     sees the warning on the invoice detail page.
        var scorer = new DocumentRiskScorer([new EtaSubmissionFailedRule()]);
        var findings = scorer.Score(new DocumentRiskContext(
            posted, new Dictionary<Guid, Item>(), refreshed, nowUtc));
        findings.Should().ContainSingle(f => f.RuleId == "ETA.SUBMISSION_FAILED",
            because: "the Tax Risk Score badge MUST surface ETA failures so the operator notices without opening the dashboard");

        // 5 — Audit log carries both the post AND the failed submission.
        //     Either missing would constitute a silent failure per
        //     FR-028 + spec edge case "Mock ETA submission failure".
        auditCapture.Captured.Should().Contain(e => e.Kind == "sales_invoice.posted");
        auditCapture.Captured.Should().Contain(e => e.Kind == "eta_submission.failed");
        auditCapture.Captured.Should().Contain(e => e.Kind == "eta_submission.retry_attempted");
        auditCapture.Captured.Should().Contain(e => e.Kind == "eta_submission.retry_failed");
    }

    private static async Task<(Customer customer, Item item, VatCategory vat)> SeedMasterDataAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true);
        var customer = new Customer(
            code: "CUST-001",
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(
                display: new ArabicEnglishText("القاهرة", "Cairo"),
                governorate: "Cairo", regionCity: "Downtown", street: "Tahrir", buildingNumber: "1"),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                tin: EgyptianTin.Parse("987654321"), vatExemption: false, defaultSalesVatCategoryId: vat.Id));
        var item = new Item(
            code: "ITEM-001",
            name: new ArabicEnglishText("ساعة", "Hour"),
            defaultVatCategoryId: vat.Id);
        db.Add(vat); db.Add(customer); db.Add(item);
        await db.SaveChangesAsync();
        return (customer, item, vat);
    }

    private static async Task SeedCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync()) return;
        db.Add(new Company(
            legalName: new ArabicEnglishText("شركة", "Company"),
            taxRegistrationNumber: EgyptianTin.Parse("123456789"),
            commercialRegistrationNumber: "CR-001234",
            address: PostalAddress.Create(
                display: new ArabicEnglishText("القاهرة", "Cairo"),
                governorate: "Cairo", regionCity: "Downtown", street: "Tahrir", buildingNumber: "12",
                postalCode: "11511"),
            taxpayerActivityCode: "0001"));
        await db.SaveChangesAsync();
    }

    private static async Task<User> SeedOperatorUserAsync(AppDbContext db)
    {
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Operator"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
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
