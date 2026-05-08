using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Journals;

/// <summary>
/// T164 / US4 scenario 1 — the canonical sales-side auto-emit
/// contract. Posting a sales invoice with subtotal 1,000 EGP +
/// VAT 140 EGP MUST produce a balanced journal entry:
///
///   * DR AccountsReceivable  1,140
///   * CR SalesRevenue        1,000
///   * CR OutputVatPayable      140
///
/// The deeper sales-emitter coverage (mixed rates, credit notes,
/// per-line discount apportionment) lives in
/// <c>SalesInvoiceJournalEmissionTests</c>; this file pins the
/// US4-acceptance-scenario-1 numbers exactly so a regression that
/// flips the dr/cr sides or rounds wrong is caught with one
/// readable test.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SalesPostingJournalTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task PostedSalesInvoice_1000Plus140Vat_Emits_AR_Revenue_OutputVat_BalancedJournal()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedAsync(db);

        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 9));
        draft.AddLine(item.Id, quantity: 1m, unitPrice: MoneyEgp.From(1_000m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var emitter = new SalesInvoiceJournalEmitter(db);
        var handler = new PostSalesInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), clock,
            new CaptureAuditLogStore(), emitter);
        var posted = await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, user.Id), CancellationToken.None);

        // Reload the journal entry that was emitted alongside the post.
        db.ChangeTracker.Clear();
        var je = await db.Set<JournalEntry>().AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);

        je.Lines.Sum(l => l.Debit.Amount).Should().Be(1_140m);
        je.Lines.Sum(l => l.Credit.Amount).Should().Be(1_140m,
            because: "US4 scenario 1 — debits MUST equal credits to the cent");

        var ar = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsReceivable);
        var revenue = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.SalesRevenue);
        var outputVat = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.OutputVatPayable);

        ar.Debit.Amount.Should().Be(1_140m);
        ar.Credit.Amount.Should().Be(0m);
        revenue.Debit.Amount.Should().Be(0m);
        revenue.Credit.Amount.Should().Be(1_000m);
        outputVat.Debit.Amount.Should().Be(0m);
        outputVat.Credit.Amount.Should().Be(140m);
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
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(customer); db.Add(item); db.Add(user);
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
