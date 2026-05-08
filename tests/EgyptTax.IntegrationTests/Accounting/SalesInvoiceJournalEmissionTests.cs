using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Accounting;

/// <summary>
/// T095 — automatic balanced journal generation on post. Per the
/// spec example: a 1,000 EGP @ 14% sales invoice produces three
/// journal lines that net to zero:
///   * DEBIT  Accounts Receivable (1200)  1,140.00
///   * CREDIT Sales Revenue        (4000)  1,000.00
///   * CREDIT Output VAT Payable   (2110)    140.00
/// And for a credit note (signs reversed): DEBIT 4000 1000 +
/// DEBIT 2110 140 + CREDIT 1200 1140. The full JournalVoucher
/// aggregate lands in US4; this emitter ships now so US1 has a
/// balanced books view from day one.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SalesInvoiceJournalEmissionTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task PostSalesInvoice_Emits_ThreeBalancedJournalLines()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (invoice, _) = await PostInvoiceWithJournalAsync(db);

        var entry = await db.Set<JournalEntry>()
            .Include(e => e.Lines)
            .AsNoTracking()
            .FirstAsync(e => e.SourceDocumentId == invoice.Id);

        entry.Lines.Should().HaveCount(3);
        entry.SourceDocumentNumber.Should().Be(invoice.DocumentNumber);
        entry.SourceDocumentType.Should().Be(DocumentType.SalesInvoice);

        var ar = entry.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsReceivable);
        var revenue = entry.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.SalesRevenue);
        var vat = entry.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.OutputVatPayable);

        ar.Debit.Amount.Should().Be(1_140m, because: "AR receives the grand total");
        ar.Credit.Amount.Should().Be(0m);
        revenue.Credit.Amount.Should().Be(1_000m, because: "Revenue is credited with the subtotal");
        revenue.Debit.Amount.Should().Be(0m);
        vat.Credit.Amount.Should()
            .Be(140m, because: "Output VAT Payable is credited with the VAT total");
        vat.Debit.Amount.Should().Be(0m);

        var sumDebits = entry.Lines.Sum(l => l.Debit.Amount);
        var sumCredits = entry.Lines.Sum(l => l.Credit.Amount);
        sumDebits
            .Should()
            .Be(
                sumCredits,
                because: "the journal MUST be balanced — sum(debits) == sum(credits) is the bedrock invariant of double-entry books"
            );
    }

    [Fact]
    public async Task PostCreditNote_Emits_ReversedJournalLines()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (originalInvoice, dependencies) = await PostInvoiceWithJournalAsync(db);

        // Issue a full credit note against the posted invoice and post it.
        var clock = new TestClock(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));
        var auditCapture = new CaptureAuditLogStore();
        var issueHandler = new IssueCreditNoteHandler(db, clock, auditCapture);
        var draft = await issueHandler.HandleAsync(
            new IssueCreditNoteCommand(
                originalInvoice.Id,
                new[]
                {
                    new IssueCreditNoteLine(
                        dependencies.Item.Id,
                        -1m,
                        MoneyEgp.From(1_000m),
                        dependencies.Vat.Id,
                        dependencies.Vat.RatePercent
                    ),
                },
                Reason: "Customer returned the goods.",
                DocumentDate: new DateOnly(2026, 5, 8)
            ),
            CancellationToken.None
        );

        var allocator = new SqlSequentialNumberAllocator(db);
        var emitter = new SalesInvoiceJournalEmitter(db);
        var postHandler = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture, emitter);
        var creditNotePosted = await postHandler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, dependencies.OperatorUser.Id),
            CancellationToken.None
        );

        var entry = await db.Set<JournalEntry>()
            .Include(e => e.Lines)
            .AsNoTracking()
            .FirstAsync(e => e.SourceDocumentId == creditNotePosted.Id);

        entry.SourceDocumentType.Should().Be(DocumentType.CreditNote);
        entry.Lines.Should().HaveCount(3);

        var ar = entry.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsReceivable);
        var revenue = entry.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.SalesRevenue);
        var vat = entry.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.OutputVatPayable);

        // Signs reversed: AR is now CREDITED (releasing the receivable),
        // Revenue is now DEBITED (reducing recorded sales), Output VAT
        // is now DEBITED (clawing back what we owed).
        ar.Credit.Amount.Should().Be(1_140m);
        ar.Debit.Amount.Should().Be(0m);
        revenue.Debit.Amount.Should().Be(1_000m);
        revenue.Credit.Amount.Should().Be(0m);
        vat.Debit.Amount.Should().Be(140m);
        vat.Credit.Amount.Should().Be(0m);

        var sumDebits = entry.Lines.Sum(l => l.Debit.Amount);
        var sumCredits = entry.Lines.Sum(l => l.Credit.Amount);
        sumDebits.Should().Be(sumCredits, because: "the credit-note journal MUST also be balanced");
    }

    [Fact]
    public async Task JournalEntry_AndOriginalInvoice_CommitInTheSameTransactionAsThePost()
    {
        // Defense-in-depth: if the journal emit blows up, the post
        // itself MUST roll back. The wrapper emits inside the same
        // SaveChangesAsync as the invoice's MarkPosted state change.
        await using var db = await _fixture.CreateContextAsync();
        var (invoice, _) = await PostInvoiceWithJournalAsync(db);

        var hasJournal = await db.Set<JournalEntry>()
            .AnyAsync(e => e.SourceDocumentId == invoice.Id);
        hasJournal.Should().BeTrue(because: "the journal entry persists alongside the post");

        var posted = await db.Set<SalesInvoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == invoice.Id);
        posted.State.Should().Be(DocumentState.Posted);
    }

    [Fact]
    public async Task TwoLineMixedRate_StillProducesBalancedJournal()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, standardVat, operatorUser) = await SeedAsync(db);

        var zeroVat = new VatCategory(
            code: "ZeroRated",
            name: new ArabicEnglishText("صفر", "Zero rated"),
            ratePercent: 0m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        db.Add(zeroVat);
        await db.SaveChangesAsync();

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(600m), standardVat.Id, standardVat.RatePercent);
        draft.AddLine(item.Id, 1m, MoneyEgp.From(400m), zeroVat.Id, zeroVat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var allocator = new SqlSequentialNumberAllocator(db);
        var emitter = new SalesInvoiceJournalEmitter(db);
        var auditCapture = new CaptureAuditLogStore();
        var handler = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture, emitter);
        var posted = await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );

        // Subtotal 1000 (600+400), VAT 84 (600 × 14%), grand 1084.
        posted.Subtotal.Amount.Should().Be(1_000m);
        posted.VatTotal.Amount.Should().Be(84m);
        posted.GrandTotal.Amount.Should().Be(1_084m);

        var entry = await db.Set<JournalEntry>()
            .Include(e => e.Lines)
            .AsNoTracking()
            .FirstAsync(e => e.SourceDocumentId == posted.Id);
        entry.Lines.Sum(l => l.Debit.Amount).Should().Be(1_084m);
        entry.Lines.Sum(l => l.Credit.Amount).Should().Be(1_084m);
        entry
            .Lines.Single(l => l.AccountCode == ChartOfAccountCodes.OutputVatPayable)
            .Credit.Amount.Should()
            .Be(84m);
    }

    private static async Task<(SalesInvoice posted, FixtureDeps deps)> PostInvoiceWithJournalAsync(
        AppDbContext db
    )
    {
        var (customer, item, vat, operatorUser) = await SeedAsync(db);
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
        var emitter = new SalesInvoiceJournalEmitter(db);
        var auditCapture = new CaptureAuditLogStore();
        var handler = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture, emitter);
        var posted = await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );
        return (posted, new FixtureDeps(customer, item, vat, operatorUser));
    }

    private sealed record FixtureDeps(
        Customer Customer,
        Item Item,
        VatCategory Vat,
        User OperatorUser
    );

    private static async Task<(Customer, Item, VatCategory, User)> SeedAsync(AppDbContext db)
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
        var company = new Company(
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
        db.Add(company);
        db.Add(user);
        await db.SaveChangesAsync();
        return (customer, item, vat, user);
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
