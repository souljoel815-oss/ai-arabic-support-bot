using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Invoices;

/// <summary>
/// T125a — FR-013 credit-note correction path. Issuing a credit note
/// against a posted sales invoice MUST: produce a credit-note
/// document that references the original; copy the customer +
/// snapshot the customer tax profile; copy lines with negated
/// quantities (signs reversed); allow partial credit (caller picks
/// the line quantities); capture a free-text reason; and on post
/// allocate a document number from the CN series. The credit note's
/// totals are negative — when summed across all posted documents in
/// the VAT period this reduces VAT payable on the next monthly VAT
/// return without any special handling.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class IssueCreditNoteTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task FullCredit_AgainstPostedInvoice_ProducesNegatedTotals_AndReferencesOriginal()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (original, customer, item, vat, operatorUser) = await PostOriginalAsync(db);

        // Sanity: original is the canonical 1000 EGP @ 14% baseline.
        original.Subtotal.Amount.Should().Be(1_000m);
        original.VatTotal.Amount.Should().Be(140m);
        original.GrandTotal.Amount.Should().Be(1_140m);

        var clock = new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));
        var auditCapture = new CaptureAuditLogStore();
        var handler = new IssueCreditNoteHandler(db, clock, auditCapture);

        var creditNoteDraft = await handler.HandleAsync(
            new IssueCreditNoteCommand(
                OriginalSalesInvoiceId: original.Id,
                Lines: new[]
                {
                    // Full credit: same line as the original with the qty negated.
                    new IssueCreditNoteLine(
                        item.Id,
                        Quantity: -1m,
                        UnitPrice: MoneyEgp.From(1_000m),
                        VatCategoryId: vat.Id,
                        VatRatePercent: vat.RatePercent
                    ),
                },
                Reason: "Customer returned the unused consulting hours after cancellation.",
                DocumentDate: new DateOnly(2026, 5, 8)
            ),
            CancellationToken.None
        );

        creditNoteDraft.IsCreditNote.Should().BeTrue();
        creditNoteDraft.CreditNoteOfInvoiceId.Should().Be(original.Id);
        creditNoteDraft.CreditNoteReason.Should().Contain("Customer returned");
        creditNoteDraft.CustomerId.Should().Be(original.CustomerId);
        creditNoteDraft.State.Should().Be(DocumentState.Draft);

        creditNoteDraft.Subtotal.Amount.Should().Be(-1_000m);
        creditNoteDraft.VatTotal.Amount.Should().Be(-140m);
        creditNoteDraft.GrandTotal.Amount.Should().Be(-1_140m);

        creditNoteDraft.Lines.Single().Quantity.Should().Be(-1m);
        creditNoteDraft.Lines.Single().LineSubtotal.Amount.Should().Be(-1_000m);
        creditNoteDraft.Lines.Single().LineVat.Amount.Should().Be(-140m);

        auditCapture.Captured.Should().ContainSingle(e => e.Kind == "credit_note.issued");
    }

    [Fact]
    public async Task PartialCredit_AllowsAQuantityLessThanOriginal()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (original, customer, item, vat, _) = await PostOriginalAsync(db);

        var clock = new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));
        var handler = new IssueCreditNoteHandler(db, clock, new CaptureAuditLogStore());

        // Original was 1 × 1000; partial credit of 0.5 × 1000 (half).
        var draft = await handler.HandleAsync(
            new IssueCreditNoteCommand(
                OriginalSalesInvoiceId: original.Id,
                Lines: new[]
                {
                    new IssueCreditNoteLine(
                        item.Id,
                        Quantity: -0.5m,
                        UnitPrice: MoneyEgp.From(1_000m),
                        VatCategoryId: vat.Id,
                        VatRatePercent: vat.RatePercent
                    ),
                },
                Reason: "Half of the consulting hours unused.",
                DocumentDate: new DateOnly(2026, 5, 8)
            ),
            CancellationToken.None
        );

        draft.Subtotal.Amount.Should().Be(-500m);
        draft.VatTotal.Amount.Should().Be(-70m);
        draft.GrandTotal.Amount.Should().Be(-570m);
    }

    [Fact]
    public async Task PostedCreditNote_AllocatesNumberFromCnSeries()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (original, customer, item, vat, operatorUser) = await PostOriginalAsync(db);

        var clock = new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));
        var auditCapture = new CaptureAuditLogStore();
        var draft = await new IssueCreditNoteHandler(db, clock, auditCapture).HandleAsync(
            new IssueCreditNoteCommand(
                original.Id,
                new[]
                {
                    new IssueCreditNoteLine(
                        item.Id,
                        -1m,
                        MoneyEgp.From(1_000m),
                        vat.Id,
                        vat.RatePercent
                    ),
                },
                Reason: "Full credit.",
                DocumentDate: new DateOnly(2026, 5, 8)
            ),
            CancellationToken.None
        );

        var allocator = new SqlSequentialNumberAllocator(db);
        var postHandler = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture);
        var posted = await postHandler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );

        posted
            .DocumentNumber.Should()
            .StartWith(
                "CN-",
                because: "credit notes MUST allocate from the CN series, not INV — derived from IsCreditNote"
            );
        posted.State.Should().Be(DocumentState.Posted);
    }

    [Fact]
    public async Task CreditNote_CannotBeIssued_IfSourceInvoice_IsNotPosted()
    {
        // T125d — FR-013 + FR-027 invariant: only Posted documents
        // are eligible source invoices. Drafts, Submitted, Approved,
        // Voided are all rejected with a clear message.
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        await SeedCompanyAsync(db);

        var draftSource = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draftSource.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draftSource);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));
        var handler = new IssueCreditNoteHandler(db, clock, new CaptureAuditLogStore());

        var act = async () =>
            await handler.HandleAsync(
                new IssueCreditNoteCommand(
                    OriginalSalesInvoiceId: draftSource.Id,
                    Lines: new[]
                    {
                        new IssueCreditNoteLine(
                            item.Id,
                            -1m,
                            MoneyEgp.From(1_000m),
                            vat.Id,
                            vat.RatePercent
                        ),
                    },
                    Reason: "Should be rejected.",
                    DocumentDate: new DateOnly(2026, 5, 8)
                ),
                CancellationToken.None
            );

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should()
            .Contain(
                "Posted",
                because: "FR-013 + FR-027 — only Posted source invoices are eligible; the rejection message MUST explain why"
            );
    }

    [Fact]
    public async Task CreditNote_CannotBeIssued_AgainstAnotherCreditNote()
    {
        // Defense-in-depth — credit note of a credit note is non-sensical
        // and would unwind the audit trail.
        await using var db = await _fixture.CreateContextAsync();
        var (original, customer, item, vat, operatorUser) = await PostOriginalAsync(db);

        var clock = new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));
        var auditCapture = new CaptureAuditLogStore();
        var issueHandler = new IssueCreditNoteHandler(db, clock, auditCapture);

        var firstCreditDraft = await issueHandler.HandleAsync(
            new IssueCreditNoteCommand(
                original.Id,
                new[]
                {
                    new IssueCreditNoteLine(
                        item.Id,
                        -1m,
                        MoneyEgp.From(1_000m),
                        vat.Id,
                        vat.RatePercent
                    ),
                },
                Reason: "Initial credit.",
                DocumentDate: new DateOnly(2026, 5, 8)
            ),
            CancellationToken.None
        );

        var allocator = new SqlSequentialNumberAllocator(db);
        var postedCredit = await new PostSalesInvoiceHandler(
            db,
            allocator,
            clock,
            auditCapture
        ).HandleAsync(
            new PostSalesInvoiceCommand(firstCreditDraft.Id, operatorUser.Id),
            CancellationToken.None
        );

        var act = async () =>
            await issueHandler.HandleAsync(
                new IssueCreditNoteCommand(
                    OriginalSalesInvoiceId: postedCredit.Id,
                    Lines: new[]
                    {
                        new IssueCreditNoteLine(
                            item.Id,
                            1m,
                            MoneyEgp.From(1_000m),
                            vat.Id,
                            vat.RatePercent
                        ),
                    },
                    Reason: "Trying to credit a credit.",
                    DocumentDate: new DateOnly(2026, 5, 8)
                ),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>(
                because: "credit note of a credit note is non-sensical"
            );
    }

    [Fact]
    public async Task CreditNote_RequiresNonEmptyReason()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (original, customer, item, vat, _) = await PostOriginalAsync(db);

        var handler = new IssueCreditNoteHandler(
            db,
            new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        );

        var act = async () =>
            await handler.HandleAsync(
                new IssueCreditNoteCommand(
                    original.Id,
                    new[]
                    {
                        new IssueCreditNoteLine(
                            item.Id,
                            -1m,
                            MoneyEgp.From(1_000m),
                            vat.Id,
                            vat.RatePercent
                        ),
                    },
                    Reason: "",
                    DocumentDate: new DateOnly(2026, 5, 8)
                ),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<ArgumentException>(
                because: "FR-013 — the reason field on a credit note is the legally-required justification for the correction; empty string is not acceptable"
            );
    }

    private static async Task<(
        SalesInvoice original,
        Customer customer,
        Item item,
        VatCategory vat,
        User operatorUser
    )> PostOriginalAsync(AppDbContext db)
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
        var handler = new PostSalesInvoiceHandler(db, allocator, clock, new CaptureAuditLogStore());
        var posted = await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );
        return (posted, customer, item, vat, operatorUser);
    }

    private static async Task<(Customer, Item, VatCategory)> SeedMasterDataAsync(AppDbContext db)
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
            code: "ITEM-001",
            name: new ArabicEnglishText("ساعة", "Hour"),
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
        if (await db.Set<Company>().AnyAsync())
            return;
        db.Add(
            new Company(
                legalName: new ArabicEnglishText("شركة", "Company"),
                taxRegistrationNumber: EgyptianTin.Parse("123456789"),
                commercialRegistrationNumber: "CR-1",
                address: PostalAddress.Create(
                    new ArabicEnglishText("القاهرة", "Cairo"),
                    "Cairo",
                    "Downtown",
                    "Tahrir",
                    "12"
                ),
                taxpayerActivityCode: "0001"
            )
        );
        await db.SaveChangesAsync();
    }

    private static async Task<User> SeedOperatorUserAsync(AppDbContext db)
    {
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
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
