using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Workflow;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Invoices;

/// <summary>
/// T081 — US1 acceptance scenario 2 / FR-012 / FR-027 / INV-002:
/// once a sales invoice is Posted, it is immutable. Any attempt to
/// edit, void, or delete it MUST be rejected with a message that
/// surfaces the FR-013 credit-note correction path so the operator
/// (and the future Blazor UI surfacing the message) knows what to do
/// instead. This test drives the guard end-to-end against a real
/// posted invoice persisted via Testcontainers, then re-loaded — so
/// configuration drift in EF mapping or state-tracking is caught at
/// the same boundary as the application layer.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PostedImmutabilityTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Posted_SalesInvoice_RejectsAddLine_AtEntityBoundary()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostInvoiceAsync(db);

        var act = () =>
            posted.AddLine(
                itemId: Guid.NewGuid(),
                quantity: 1m,
                unitPrice: MoneyEgp.From(50m),
                vatCategoryId: Guid.NewGuid(),
                vatRatePercent: 14m
            );

        act.Should()
            .Throw<InvalidOperationException>(
                because: "the entity-level invariant rejects mutation of any non-Draft state per the SalesInvoice aggregate root design"
            );
    }

    [Fact]
    public async Task Posted_SalesInvoice_GuardRejectsEdit_WithCreditNoteHint()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostInvoiceAsync(db);

        var act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                state: posted.State,
                documentType: DocumentType.SalesInvoice,
                documentId: posted.Id,
                operation: "edit"
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*credit note*",
                because: "FR-012 + FR-013 — the rejection MUST surface the credit-note correction path for tax-impacting documents"
            );
    }

    [Fact]
    public async Task Posted_SalesInvoice_GuardRejectsDelete_WithCreditNoteHint()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostInvoiceAsync(db);

        var act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                state: posted.State,
                documentType: DocumentType.SalesInvoice,
                documentId: posted.Id,
                operation: "delete"
            );

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should()
            .Contain(
                "credit note (FR-013)",
                because: "tax-impacting documents are corrected via credit note per FR-013"
            );
        ex.Message.Should()
            .Contain(
                "delete",
                because: "the message MUST name the rejected operation so the audit trail is unambiguous"
            );
        ex.Message.Should()
            .Contain(
                posted.Id.ToString("D"),
                because: "the message MUST identify the document so an operator can locate it"
            );
    }

    [Fact]
    public async Task Posted_SalesInvoice_GuardRejectsVoid_PerFr027()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostInvoiceAsync(db);

        var act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                state: posted.State,
                documentType: DocumentType.SalesInvoice,
                documentId: posted.Id,
                operation: "void"
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Posted and immutable*",
                because: "FR-027 — voiding a Posted document is forbidden; only the credit-note path corrects it"
            );
    }

    [Fact]
    public async Task Reloaded_PostedInvoice_StillRejectsMutation()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostInvoiceAsync(db);
        var invoiceId = posted.Id;

        // Round-trip through the database — no in-memory state could
        // mask a regression where the State column wasn't persisted as
        // Posted.
        db.ChangeTracker.Clear();
        var reloaded = await db.Set<SalesInvoice>()
            .Include(i => i.Lines)
            .FirstAsync(i => i.Id == invoiceId);

        reloaded
            .State.Should()
            .Be(
                DocumentState.Posted,
                because: "the State column MUST round-trip as Posted after T080's posting flow"
            );

        var act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                reloaded.State,
                DocumentType.SalesInvoice,
                reloaded.Id,
                "edit"
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*credit note*");
    }

    [Fact]
    public async Task Posted_SalesInvoice_StateMachine_RejectsTransitionAwayFromPosted()
    {
        await using var db = await _fixture.CreateContextAsync();
        var posted = await PostInvoiceAsync(db);

        // FR-026: Posted is terminal regardless of approval setting.
        DocumentStateMachine
            .CanTransition(posted.State, DocumentState.Voided, approvalEnabled: false)
            .Should()
            .BeFalse();
        DocumentStateMachine
            .CanTransition(posted.State, DocumentState.Voided, approvalEnabled: true)
            .Should()
            .BeFalse();
        DocumentStateMachine
            .CanTransition(posted.State, DocumentState.Draft, approvalEnabled: false)
            .Should()
            .BeFalse();
    }

    private static async Task<SalesInvoice> PostInvoiceAsync(AppDbContext db)
    {
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        var operatorUser = await SeedOperatorUserAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customerId: customer.Id,
            customerTaxProfileSnapshot: customer.TaxProfile,
            documentDate: new DateOnly(2026, 5, 7)
        );
        draft.AddLine(
            itemId: item.Id,
            quantity: 1m,
            unitPrice: MoneyEgp.From(1_000m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent
        );
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));
        var allocator = new SqlSequentialNumberAllocator(db);
        var auditCapture = new CaptureAuditLogStore();
        var handler = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture);

        return await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );
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
