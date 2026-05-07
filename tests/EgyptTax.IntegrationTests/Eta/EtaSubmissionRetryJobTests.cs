using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.BackgroundJobs;
using EgyptTax.Infrastructure.Eta;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Eta;

/// <summary>
/// T099 — FR-036 retry job. The recurring Hangfire job MUST:
///   * pick up <c>Failed</c> rows whose
///     <c>SubmissionWindowExpiresAtUtc</c> is still in the future,
///   * leave <c>Submitted</c> rows alone (terminal — never re-submit),
///   * leave <c>Pending</c> rows alone (a never-attempted row awaits
///     its first try, not a retry),
///   * leave expired-window <c>Failed</c> rows alone (regulator
///     deadline passed; manual operator intervention required),
///   * for each retried row, increment <c>AttemptCount</c> via
///     <c>RecordAttempt</c>, capture the new outcome (status / uuid
///     / error), persist <c>LastAttemptAtUtc</c>,
///   * emit FR-028 audit events keyed
///     <c>eta_submission.retry_attempted</c> +
///     <c>eta_submission.retry_submitted</c> on success or
///     <c>eta_submission.retry_failed</c> on failure.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class EtaSubmissionRetryJobTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task FailedRow_StillInWindow_RetrySucceeds_FlipsToSubmitted()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedMasterDataAndCompanyAsync(db);
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

        var (invoice, etaRow) = await SeedFailedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(2));

        var captureAudit = new CaptureAuditLogStore();
        var job = NewJob(db, captureAudit, nowUtc, failureRate: 0.0);

        var result = await job.RunOnceAsync(CancellationToken.None);

        result.TotalCandidates.Should().Be(1);
        result.SucceededCount.Should().Be(1);
        result.FailedCount.Should().Be(0);

        var reloaded = await db.Set<EtaSubmission>().AsNoTracking()
            .FirstAsync(s => s.Id == etaRow.Id);
        reloaded.Status.Should().Be(EtaSubmissionStatus.Submitted);
        reloaded.SubmissionUuid.Should().NotBeNullOrWhiteSpace();
        reloaded.AttemptCount.Should().Be(2,
            because: "the seeded row was created with one prior failed attempt; this retry is the second");
        reloaded.LastAttemptAtUtc.Should().Be(nowUtc);
        reloaded.ErrorCode.Should().BeNull(
            because: "the prior error fields MUST be cleared when the retry succeeds");

        captureAudit.Captured.Should().Contain(e => e.Kind == "eta_submission.retry_attempted",
            because: "every retry MUST emit a retry_attempted event before the submit so the chain captures the intent");
        captureAudit.Captured.Should().Contain(e => e.Kind == "eta_submission.retry_submitted",
            because: "successful retries fire retry_submitted (not the post-time submitted event)");
        captureAudit.Captured.Should().NotContain(e => e.Kind == "eta_submission.retry_failed");
    }

    [Fact]
    public async Task FailedRow_StillInWindow_RetryFails_StaysFailedAndRecordsErrorCode()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedMasterDataAndCompanyAsync(db);
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

        var (invoice, etaRow) = await SeedFailedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(2));

        var captureAudit = new CaptureAuditLogStore();
        var job = NewJob(db, captureAudit, nowUtc, failureRate: 1.0);

        var result = await job.RunOnceAsync(CancellationToken.None);

        result.TotalCandidates.Should().Be(1);
        result.SucceededCount.Should().Be(0);
        result.FailedCount.Should().Be(1);

        var reloaded = await db.Set<EtaSubmission>().AsNoTracking().FirstAsync(s => s.Id == etaRow.Id);
        reloaded.Status.Should().Be(EtaSubmissionStatus.Failed);
        reloaded.ErrorCode.Should().Be("ETA_MOCK_500");
        reloaded.AttemptCount.Should().Be(2);

        captureAudit.Captured.Should().Contain(e => e.Kind == "eta_submission.retry_failed");
        captureAudit.Captured.Should().NotContain(e => e.Kind == "eta_submission.retry_submitted");
    }

    [Fact]
    public async Task SubmittedRows_AreNeverRetried()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedMasterDataAndCompanyAsync(db);
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

        var (_, etaRow) = await SeedSubmittedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(2));
        var originalAttemptCount = etaRow.AttemptCount;
        var originalUuid = etaRow.SubmissionUuid;

        var captureAudit = new CaptureAuditLogStore();
        var job = NewJob(db, captureAudit, nowUtc, failureRate: 0.0);

        var result = await job.RunOnceAsync(CancellationToken.None);

        result.TotalCandidates.Should().Be(0,
            because: "the query MUST filter on status='Failed' so Submitted rows never even land in the candidate list");

        var reloaded = await db.Set<EtaSubmission>().AsNoTracking().FirstAsync(s => s.Id == etaRow.Id);
        reloaded.Status.Should().Be(EtaSubmissionStatus.Submitted);
        reloaded.AttemptCount.Should().Be(originalAttemptCount,
            because: "Submitted is terminal — RecordAttempt would throw, but we MUST NOT even call it");
        reloaded.SubmissionUuid.Should().Be(originalUuid);

        captureAudit.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task PendingRows_AreNeverRetried()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedMasterDataAndCompanyAsync(db);
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

        var (_, etaRow) = await SeedPendingInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(2));

        var captureAudit = new CaptureAuditLogStore();
        var job = NewJob(db, captureAudit, nowUtc, failureRate: 0.0);

        var result = await job.RunOnceAsync(CancellationToken.None);

        result.TotalCandidates.Should().Be(0,
            because: "Pending rows await their first attempt (owned by the post-time wrapper handler); the retry job MUST NOT pick them up");

        var reloaded = await db.Set<EtaSubmission>().AsNoTracking().FirstAsync(s => s.Id == etaRow.Id);
        reloaded.Status.Should().Be(EtaSubmissionStatus.Pending);
        reloaded.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task FailedRows_ExpiredWindow_AreNeverRetried_ButRemainInDatabase()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedMasterDataAndCompanyAsync(db);
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

        var (_, etaRow) = await SeedFailedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(-1));

        var captureAudit = new CaptureAuditLogStore();
        var job = NewJob(db, captureAudit, nowUtc, failureRate: 0.0);

        var result = await job.RunOnceAsync(CancellationToken.None);

        result.TotalCandidates.Should().Be(0,
            because: "expired-deadline Failed rows are past the regulator's submission cutoff; the retry job MUST NOT auto-attempt — manual operator intervention required");

        var reloaded = await db.Set<EtaSubmission>().AsNoTracking().FirstAsync(s => s.Id == etaRow.Id);
        reloaded.Status.Should().Be(EtaSubmissionStatus.Failed,
            because: "the expired Failed row MUST remain in the database — the dashboard's expired-tile counts these so they don't silently disappear");
        reloaded.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task MixedFixture_RetriesOnlyTheRightRows()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedMasterDataAndCompanyAsync(db);
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

        // 3 Failed rows still in window — eligible
        var (_, eligible1) = await SeedFailedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(1));
        var (_, eligible2) = await SeedFailedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(3));
        var (_, eligible3) = await SeedFailedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(5));

        // 1 Failed row past window — NOT eligible
        var (_, expired) = await SeedFailedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromHours(-2));

        // 1 Submitted row — NOT eligible
        var (_, submitted) = await SeedSubmittedInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(1));

        // 1 Pending row — NOT eligible
        var (_, pending) = await SeedPendingInvoiceAsync(db, nowUtc, deadlineOffset: TimeSpan.FromDays(1));

        var captureAudit = new CaptureAuditLogStore();
        var job = NewJob(db, captureAudit, nowUtc, failureRate: 0.0);

        var result = await job.RunOnceAsync(CancellationToken.None);

        result.TotalCandidates.Should().Be(3);
        result.SucceededCount.Should().Be(3);
        result.FailedCount.Should().Be(0);

        var snapshot = await db.Set<EtaSubmission>().AsNoTracking().ToListAsync();
        snapshot.Should().HaveCount(6);
        snapshot.Single(s => s.Id == eligible1.Id).Status.Should().Be(EtaSubmissionStatus.Submitted);
        snapshot.Single(s => s.Id == eligible2.Id).Status.Should().Be(EtaSubmissionStatus.Submitted);
        snapshot.Single(s => s.Id == eligible3.Id).Status.Should().Be(EtaSubmissionStatus.Submitted);
        snapshot.Single(s => s.Id == expired.Id).Status.Should().Be(EtaSubmissionStatus.Failed);
        snapshot.Single(s => s.Id == submitted.Id).Status.Should().Be(EtaSubmissionStatus.Submitted);
        snapshot.Single(s => s.Id == pending.Id).Status.Should().Be(EtaSubmissionStatus.Pending);

        captureAudit.Captured.Count(e => e.Kind == "eta_submission.retry_attempted").Should().Be(3);
        captureAudit.Captured.Count(e => e.Kind == "eta_submission.retry_submitted").Should().Be(3);
    }

    private static EtaSubmissionRetryJob NewJob(
        AppDbContext db,
        IAuditLogStore auditLog,
        DateTime nowUtc,
        double failureRate)
    {
        var clock = new FixedClock(nowUtc);
        var submitter = new MockEtaSubmitter(failureRate);
        var jsonGenerator = new EInvoiceJsonGenerator();
        return new EtaSubmissionRetryJob(db, submitter, jsonGenerator, auditLog, clock);
    }

    private static async Task<(SalesInvoice invoice, EtaSubmission eta)>
        SeedFailedInvoiceAsync(AppDbContext db, DateTime nowUtc, TimeSpan deadlineOffset)
    {
        var (customer, item, vat) = await GetMasterDataAsync(db);
        var invoice = await BuildPostedInvoiceAsync(db, customer, item, vat, nowUtc);

        var eta = await db.Set<EtaSubmission>().FirstAsync(s => s.SalesInvoiceId == invoice.Id);
        eta.RecordAttempt(EtaSubmissionStatus.Failed, submissionUuid: null,
            errorCode: "ETA_MOCK_500", errorMessage: "First attempt failed.", nowUtc: nowUtc.AddMinutes(-5));
        // Force the deadline to the test-controlled value via direct
        // EF column update — the entity's deadline is init-only, so
        // we use ExecuteUpdate to set it deterministically per test.
        var newDeadline = nowUtc.Add(deadlineOffset);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [eta].[eta_submissions] SET [submission_window_expires_at_utc] = @p0 WHERE [id] = @p1",
            newDeadline, eta.Id);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var refreshed = await db.Set<EtaSubmission>().AsNoTracking().FirstAsync(s => s.Id == eta.Id);
        return (invoice, refreshed);
    }

    private static async Task<(SalesInvoice invoice, EtaSubmission eta)>
        SeedSubmittedInvoiceAsync(AppDbContext db, DateTime nowUtc, TimeSpan deadlineOffset)
    {
        var (customer, item, vat) = await GetMasterDataAsync(db);
        var invoice = await BuildPostedInvoiceAsync(db, customer, item, vat, nowUtc);

        var eta = await db.Set<EtaSubmission>().FirstAsync(s => s.SalesInvoiceId == invoice.Id);
        eta.RecordAttempt(EtaSubmissionStatus.Submitted, submissionUuid: $"mock-{Guid.NewGuid():N}",
            errorCode: null, errorMessage: null, nowUtc: nowUtc.AddMinutes(-5));
        var newDeadline = nowUtc.Add(deadlineOffset);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [eta].[eta_submissions] SET [submission_window_expires_at_utc] = @p0 WHERE [id] = @p1",
            newDeadline, eta.Id);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var refreshed = await db.Set<EtaSubmission>().AsNoTracking().FirstAsync(s => s.Id == eta.Id);
        return (invoice, refreshed);
    }

    private static async Task<(SalesInvoice invoice, EtaSubmission eta)>
        SeedPendingInvoiceAsync(AppDbContext db, DateTime nowUtc, TimeSpan deadlineOffset)
    {
        var (customer, item, vat) = await GetMasterDataAsync(db);
        var invoice = await BuildPostedInvoiceAsync(db, customer, item, vat, nowUtc);

        var eta = await db.Set<EtaSubmission>().FirstAsync(s => s.SalesInvoiceId == invoice.Id);
        var newDeadline = nowUtc.Add(deadlineOffset);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [eta].[eta_submissions] SET [submission_window_expires_at_utc] = @p0 WHERE [id] = @p1",
            newDeadline, eta.Id);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var refreshed = await db.Set<EtaSubmission>().AsNoTracking().FirstAsync(s => s.Id == eta.Id);
        return (invoice, refreshed);
    }

    private static async Task<SalesInvoice> BuildPostedInvoiceAsync(
        AppDbContext db, Customer customer, Item item, VatCategory vat, DateTime nowUtc)
    {
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 7));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        draft.MarkPosted(
            documentNumber: $"INV-2026-{Math.Abs(Guid.NewGuid().GetHashCode()) % 1000000:D6}",
            postedByUserId: Guid.NewGuid(),
            postedAtUtc: nowUtc.AddMinutes(-30),
            postingMode: DocumentPostingMode.UnapprovedDirect,
            approvalEnabled: false);
        db.Add(draft);

        var eta = new EtaSubmission(salesInvoiceId: draft.Id,
            postedAtUtc: nowUtc.AddMinutes(-30), nowUtc: nowUtc.AddMinutes(-30));
        db.Add(eta);

        await db.SaveChangesAsync();
        return draft;
    }

    private static async Task<(Customer, Item, VatCategory)> GetMasterDataAsync(AppDbContext db)
    {
        var vat = await db.Set<VatCategory>().FirstAsync();
        var customer = await db.Set<Customer>().FirstAsync();
        var item = await db.Set<Item>().FirstAsync();
        return (customer, item, vat);
    }

    private static async Task SeedMasterDataAndCompanyAsync(AppDbContext db)
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
            name: new ArabicEnglishText("عميل تجريبي", "Test Customer LLC"),
            address: PostalAddress.Create(
                display: new ArabicEnglishText("القاهرة", "Cairo"),
                governorate: "Cairo", regionCity: "Downtown", street: "Tahrir", buildingNumber: "1"),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                tin: EgyptianTin.Parse("987654321"), vatExemption: false, defaultSalesVatCategoryId: vat.Id));
        var item = new Item(
            code: "ITEM-001",
            name: new ArabicEnglishText("ساعة استشارة", "Consulting Hour"),
            defaultVatCategoryId: vat.Id);
        var company = new Company(
            legalName: new ArabicEnglishText("شركة الاختبار", "Test Company SAE"),
            taxRegistrationNumber: EgyptianTin.Parse("123456789"),
            commercialRegistrationNumber: "CR-001234",
            address: PostalAddress.Create(
                display: new ArabicEnglishText("القاهرة", "Cairo"),
                governorate: "Cairo", regionCity: "Downtown", street: "Tahrir", buildingNumber: "12",
                postalCode: "11511"),
            taxpayerActivityCode: "0001");
        db.Add(vat); db.Add(customer); db.Add(item); db.Add(company);
        await db.SaveChangesAsync();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
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
            var entry = new AuditLogEntry(
                index: Captured.Count, tsUtc: DateTime.UtcNow,
                actorUserId: payload.ActorUserId, actorFirmName: payload.ActorFirmName,
                companyId: payload.CompanyId, kind: payload.Kind, payloadJson: payload.PayloadJson,
                prevHash: new byte[32], thisHash: new byte[32]);
            return Task.FromResult(entry);
        }
    }
}
