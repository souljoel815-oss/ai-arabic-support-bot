using EgyptTax.Application.Audit;
using EgyptTax.Application.Workflow;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Workflow;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Workflow;

/// <summary>
/// FR-026 / FR-004 — approval-workflow integration tests.
///   * T151 happy path: Draft → Submitted → Approved (per US3 sc 1).
///   * T152 rejection: Submitted → Draft + reason captured (sc 2).
///   * T154 self-approval forbidden (sc 4 / FR-004).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class DocumentApprovalTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task US3_HappyPath_Draft_Submitted_Approved()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, bookkeeper, approver) = await SeedAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var audit = new CaptureAuditLogStore();
        var handler = new DocumentApprovalHandler(db, clock, audit);

        // Step 1: Bookkeeper submits.
        var request = await handler.SubmitAsync(
            new SubmitDocumentCommand(draft.Id, DocumentType.SalesInvoice, bookkeeper.Id),
            CancellationToken.None
        );

        var afterSubmit = await db.Set<SalesInvoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == draft.Id);
        afterSubmit
            .State.Should()
            .Be(
                DocumentState.Submitted,
                because: "Submit transition MUST move Draft → Submitted per FR-026"
            );

        // Step 2: Approver approves.
        await handler.ApproveAsync(
            new ApproveDocumentCommand(draft.Id, DocumentType.SalesInvoice, approver.Id),
            CancellationToken.None
        );

        var afterApprove = await db.Set<SalesInvoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == draft.Id);
        afterApprove.State.Should().Be(DocumentState.Approved);

        var freshRequest = await db.Set<ApprovalRequest>()
            .AsNoTracking()
            .FirstAsync(r => r.Id == request.Id);
        freshRequest.Status.Should().Be(ApprovalRequestStatus.Approved);
        freshRequest.ApprovedByUserId.Should().Be(approver.Id);

        audit.Captured.Should().Contain(e => e.Kind == "document.submitted_for_approval");
        audit.Captured.Should().Contain(e => e.Kind == "document.approved");
    }

    [Fact]
    public async Task US3_Rejection_Returns_To_Draft_With_Reason_Captured()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, bookkeeper, approver) = await SeedAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(500m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var audit = new CaptureAuditLogStore();
        var handler = new DocumentApprovalHandler(db, clock, audit);

        await handler.SubmitAsync(
            new SubmitDocumentCommand(draft.Id, DocumentType.SalesInvoice, bookkeeper.Id),
            CancellationToken.None
        );
        await handler.RejectAsync(
            new RejectDocumentCommand(
                draft.Id,
                DocumentType.SalesInvoice,
                approver.Id,
                "VAT category looks wrong — should be Standard, not ZeroRated."
            ),
            CancellationToken.None
        );

        var afterReject = await db.Set<SalesInvoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == draft.Id);
        afterReject
            .State.Should()
            .Be(
                DocumentState.Draft,
                because: "Reject MUST return state to Draft so the bookkeeper can edit + resubmit"
            );

        var request = await db.Set<ApprovalRequest>()
            .AsNoTracking()
            .FirstAsync(r => r.DocumentId == draft.Id);
        request.Status.Should().Be(ApprovalRequestStatus.Rejected);
        request.RejectedByUserId.Should().Be(approver.Id);
        request.RejectionReason.Should().Contain("VAT category looks wrong");

        audit
            .Captured.Single(e => e.Kind == "document.rejected")
            .PayloadJson.Should()
            .Contain(
                "VAT category looks wrong",
                because: "the rejection reason MUST land in the audit chain so the inspector can see WHY"
            );
    }

    [Fact]
    public async Task FR004_Self_Approval_Is_Forbidden()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, bookkeeper, _) = await SeedAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(100m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = new DocumentApprovalHandler(
            db,
            new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        );

        await handler.SubmitAsync(
            new SubmitDocumentCommand(draft.Id, DocumentType.SalesInvoice, bookkeeper.Id),
            CancellationToken.None
        );

        // Same user attempts to approve their own submission.
        var act = async () =>
            await handler.ApproveAsync(
                new ApproveDocumentCommand(draft.Id, DocumentType.SalesInvoice, bookkeeper.Id),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-004", StringComparison.OrdinalIgnoreCase));

        // State must NOT have transitioned despite the failed approve.
        var afterFailedApprove = await db.Set<SalesInvoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == draft.Id);
        afterFailedApprove
            .State.Should()
            .Be(
                DocumentState.Submitted,
                because: "the FR-004 guard fires inside RecordApproval BEFORE the document state is touched, so the doc stays in Submitted for the next eligible approver to handle"
            );
    }

    [Fact]
    public async Task Void_OnDraft_Succeeds_OnPosted_IsBlocked()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, bookkeeper, _) = await SeedAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(100m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var handler = new DocumentApprovalHandler(
            db,
            new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        );

        await handler.VoidAsync(
            new VoidDocumentCommand(
                draft.Id,
                DocumentType.SalesInvoice,
                bookkeeper.Id,
                "Test void"
            ),
            CancellationToken.None
        );

        var voided = await db.Set<SalesInvoice>().AsNoTracking().FirstAsync(i => i.Id == draft.Id);
        voided.State.Should().Be(DocumentState.Voided);

        // FR-027: Posted documents cannot be voided. Build a directly-
        // posted document and try to void it; the state-machine
        // throws because Posted is terminal.
        var direct = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 8)
        );
        direct.AddLine(item.Id, 1m, MoneyEgp.From(200m), vat.Id, vat.RatePercent);
        direct.MarkPosted(
            "INV-2026-TEST-1",
            bookkeeper.Id,
            new DateTime(2026, 5, 8, 11, 0, 0, DateTimeKind.Utc),
            DocumentPostingMode.UnapprovedDirect,
            approvalEnabled: false
        );
        db.Add(direct);
        await db.SaveChangesAsync();

        var voidPosted = async () =>
            await handler.VoidAsync(
                new VoidDocumentCommand(
                    direct.Id,
                    DocumentType.SalesInvoice,
                    bookkeeper.Id,
                    "should fail"
                ),
                CancellationToken.None
            );

        await voidPosted
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(
                ex => ex.Message.Contains("Posted", StringComparison.OrdinalIgnoreCase),
                because: "FR-027 — Posted is terminal; corrections route through credit notes (FR-013) or reversal vouchers"
            );
    }

    private static async Task<(
        Customer,
        Item,
        VatCategory,
        User bookkeeper,
        User approver
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
        var item = new Item(
            code: $"ITEM-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("صنف", "Item"),
            defaultVatCategoryId: vat.Id
        );
        var bookkeeper = new User(
            email: $"book-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مساعد", "Bookkeeper"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        var approver = new User(
            email: $"approve-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("معتمد", "Approver"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(vat);
        db.Add(customer);
        db.Add(item);
        db.Add(bookkeeper);
        db.Add(approver);
        await db.SaveChangesAsync();
        return (customer, item, vat, bookkeeper, approver);
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
