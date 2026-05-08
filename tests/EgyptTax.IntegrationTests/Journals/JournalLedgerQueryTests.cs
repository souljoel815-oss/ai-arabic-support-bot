using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Journals;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Journals;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Journals;

/// <summary>
/// T171 — unified journal-ledger query is the read surface behind
/// the Pages/Journals/* Razor pages. Pins three guarantees the UI
/// relies on:
///   * The list UNIONS auto-emitted JournalEntries with manual +
///     reversal JournalVouchers — the operator sees one
///     chronological stream regardless of source.
///   * The list correctly classifies each row by Kind (AutoEmitted
///     / ManualAdjusting / Reversal).
///   * The detail Get correctly resolves both flavours by
///     (id, kind), surfacing the per-line debits + credits + the
///     FR-025 drill-back metadata (SourceDocumentId for auto-
///     emitted, ReversesJournalVoucherId for reversals).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class JournalLedgerQueryTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task ListAsync_Unions_Auto_Manual_AndReversal_InOnePeriodStream()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, accountant) = await SeedAsync(db);

        // (1) Auto-emit a sales-invoice journal entry by posting an invoice.
        var clock = new TestClock(new DateTime(2026, 5, 9, 9, 0, 0, DateTimeKind.Utc));
        var emitter = new SalesInvoiceJournalEmitter(db);
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 9));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();
        await new PostSalesInvoiceHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore(), emitter)
            .HandleAsync(new PostSalesInvoiceCommand(draft.Id, accountant.Id), CancellationToken.None);

        // (2) Create a manual adjusting voucher.
        var manual = await new CreateManualAdjustingJournalHandler(db, clock, new CaptureAuditLogStore())
            .HandleAsync(new CreateManualAdjustingJournalCommand(
                Date: new DateOnly(2026, 5, 9),
                Narration: new ArabicEnglishText("تسوية", "Test manual"),
                CreatedByUserId: accountant.Id,
                Lines: new[]
                {
                    new ManualJournalLineInput("5200", MoneyEgp.From(500m), MoneyEgp.Zero, "Expense"),
                    new ManualJournalLineInput("2200", MoneyEgp.Zero, MoneyEgp.From(500m), "Payable"),
                }), CancellationToken.None);

        // (3) Reverse that manual voucher.
        var reversal = await new CreateReversalJournalHandler(db, clock, new CaptureAuditLogStore())
            .HandleAsync(new CreateReversalJournalCommand(
                OriginalJournalVoucherId: manual.Id,
                ReversalDate: new DateOnly(2026, 5, 9),
                ReversalNarration: new ArabicEnglishText("عكس", "Reverse it"),
                CreatedByUserId: accountant.Id), CancellationToken.None);

        var query = new SqlJournalLedgerQuery(db);
        var rows = await query.ListAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        rows.Should().HaveCount(3,
            because: "the list MUST union auto-emit + manual + reversal in one stream");
        rows.Should().Contain(r => r.Kind == JournalEntryKind.AutoEmitted,
            because: "the posted sales invoice produced an auto-emitted JE");
        rows.Should().Contain(r => r.Kind == JournalEntryKind.ManualAdjusting && r.Id == manual.Id);
        rows.Should().Contain(r => r.Kind == JournalEntryKind.Reversal && r.Id == reversal.Id);
    }

    [Fact]
    public async Task GetAsync_AutoEmitted_SurfacesSourceDocumentForFr025DrillBack()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, accountant) = await SeedAsync(db);

        var clock = new TestClock(new DateTime(2026, 5, 9, 9, 0, 0, DateTimeKind.Utc));
        var emitter = new SalesInvoiceJournalEmitter(db);
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 9));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();
        var posted = await new PostSalesInvoiceHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore(), emitter)
            .HandleAsync(new PostSalesInvoiceCommand(draft.Id, accountant.Id), CancellationToken.None);

        db.ChangeTracker.Clear();
        var entry = await db.Set<EgyptTax.Domain.Accounting.JournalEntry>().AsNoTracking()
            .FirstAsync(e => e.SourceDocumentId == posted.Id);

        var detail = await new SqlJournalLedgerQuery(db)
            .GetAsync(entry.Id, JournalEntryKind.AutoEmitted);

        detail.Should().NotBeNull();
        detail!.Kind.Should().Be(JournalEntryKind.AutoEmitted);
        detail.SourceDocumentType.Should().Be(DocumentType.SalesInvoice);
        detail.SourceDocumentId.Should().Be(posted.Id,
            because: "FR-025 drill-back: the detail page links back to the originating sales invoice");
        detail.SourceDocumentNumber.Should().NotBeNullOrWhiteSpace();
        detail.Lines.Should().HaveCount(3);
        detail.TotalDebits.Should().Be(detail.TotalCredits);
    }

    [Fact]
    public async Task GetAsync_Reversal_SurfacesReversesJournalVoucherIdForFr025DrillBack()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (_, _, _, accountant) = await SeedAsync(db);

        var clock = new TestClock(new DateTime(2026, 5, 9, 9, 0, 0, DateTimeKind.Utc));
        var manual = await new CreateManualAdjustingJournalHandler(db, clock, new CaptureAuditLogStore())
            .HandleAsync(new CreateManualAdjustingJournalCommand(
                Date: new DateOnly(2026, 5, 9),
                Narration: new ArabicEnglishText("أصلي", "Original"),
                CreatedByUserId: accountant.Id,
                Lines: new[]
                {
                    new ManualJournalLineInput("5200", MoneyEgp.From(300m), MoneyEgp.Zero, "Expense"),
                    new ManualJournalLineInput("2200", MoneyEgp.Zero, MoneyEgp.From(300m), "Payable"),
                }), CancellationToken.None);

        var reversal = await new CreateReversalJournalHandler(db, clock, new CaptureAuditLogStore())
            .HandleAsync(new CreateReversalJournalCommand(
                OriginalJournalVoucherId: manual.Id,
                ReversalDate: new DateOnly(2026, 5, 9),
                ReversalNarration: new ArabicEnglishText("عكس", "Reverse"),
                CreatedByUserId: accountant.Id), CancellationToken.None);

        var detail = await new SqlJournalLedgerQuery(db)
            .GetAsync(reversal.Id, JournalEntryKind.Reversal);

        detail.Should().NotBeNull();
        detail!.Kind.Should().Be(JournalEntryKind.Reversal);
        detail.ReversesJournalVoucherId.Should().Be(manual.Id,
            because: "FR-025 drill-back: the reversal detail page links back to the original voucher");
        detail.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_NonexistentId_ReturnsNull()
    {
        await using var db = await _fixture.CreateContextAsync();
        var detail = await new SqlJournalLedgerQuery(db)
            .GetAsync(Guid.NewGuid(), JournalEntryKind.AutoEmitted);
        detail.Should().BeNull();
    }

    private static async Task<(Customer, Item, VatCategory, User)> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var customer = new Customer(
            code: $"CUS-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "1"),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                vatExemption: false, defaultSalesVatCategoryId: vat.Id));
        var item = new Item(
            code: $"IT-{Guid.NewGuid():N}".Substring(0, 8),
            name: new ArabicEnglishText("بند", "Item"),
            defaultVatCategoryId: vat.Id);

        // Accountant role required by the manual + reversal handlers.
        var accountantRole = new Role(
            code: "Accountant", name: new ArabicEnglishText("محاسب", "Accountant"),
            requiresMfa: false);
        var user = new User(
            email: $"acc-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("محاسب", "Accountant"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        user.Roles.Add(accountantRole);

        db.Add(vat); db.Add(customer); db.Add(item); db.Add(accountantRole); db.Add(user);
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
