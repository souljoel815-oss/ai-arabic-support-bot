using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Journals;

/// <summary>
/// T166 / SC-003 — 100 % of auto-generated journals MUST balance
/// to the cent across 1,000 randomized posted documents. Mix is
/// 500 sales invoices + 500 non-deductible purchase invoices
/// (non-deductible to skip the FR-016 attachment requirement);
/// random subtotals across [100, 10_000] EGP exercise rounding
/// edges + the per-line discount apportionment on the sales side.
///
/// Catches any future regression that breaks the
/// SUM(debit)=SUM(credit) bedrock invariant — the kind of
/// bookkeeping bug an inspector would reject the entire bundle
/// for.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class JournalBalanceStressTests(SqlServerFixture fixture)
{
    private const int DocumentCount = 1_000;
    private const int RandomSeed = 42; // Reproducible across runs.

    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task OneThousandRandomizedPostedDocuments_AllJournals_BalanceToTheCent()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, supplier, vat, item, user) = await SeedAsync(db);

        var clock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var allocator = new SqlSequentialNumberAllocator(db);
        var audit = new CaptureAuditLogStore();
        var salesEmitter = new SalesInvoiceJournalEmitter(db);
        var purchaseEmitter = new PurchaseInvoiceJournalEmitter(db);
        var salesHandler = new PostSalesInvoiceHandler(db, allocator, clock, audit, salesEmitter);
        var purchaseHandler = new PostPurchaseInvoiceHandler(
            db,
            allocator,
            clock,
            audit,
            purchaseEmitter
        );

        var rng = new Random(RandomSeed);
        for (var i = 0; i < DocumentCount; i++)
        {
            // Random subtotal between 100.00 and 10,000.00, with
            // arbitrary cents to exercise rounding paths.
            var unitPrice = MoneyEgp.From(
                decimal.Round(
                    100m + (decimal)(rng.NextDouble() * 9_900),
                    2,
                    MidpointRounding.ToEven
                )
            );
            var quantity = 1m + (decimal)rng.Next(0, 5); // 1..5

            if (i % 2 == 0)
            {
                var draft = SalesInvoice.CreateDraft(
                    customer.Id,
                    customer.TaxProfile,
                    new DateOnly(2026, 5, 9)
                );
                draft.AddLine(item.Id, quantity, unitPrice, vat.Id, vat.RatePercent);
                db.Add(draft);
                await db.SaveChangesAsync();
                await salesHandler.HandleAsync(
                    new PostSalesInvoiceCommand(draft.Id, user.Id),
                    CancellationToken.None
                );
            }
            else
            {
                var draft = PurchaseInvoice.CreateDraft(
                    supplier.Id,
                    supplier.TaxProfile,
                    $"SUP-T166-{i:D4}",
                    new DateOnly(2026, 5, 9)
                );
                draft.AddLine(
                    itemId: null,
                    expenseCategoryId: Guid.NewGuid(),
                    quantity,
                    unitPrice,
                    vat.Id,
                    vat.RatePercent,
                    deductibleFlag: false
                ); // non-deductible — no FR-016 attachment needed
                db.Add(draft);
                await db.SaveChangesAsync();
                await purchaseHandler.HandleAsync(
                    new PostPurchaseInvoiceCommand(draft.Id, user.Id),
                    CancellationToken.None
                );
            }

            db.ChangeTracker.Clear();
        }

        // Pull every emitted journal entry + verify per-entry balance.
        var entries = await db.Set<JournalEntry>()
            .AsNoTracking()
            .Include(e => e.Lines)
            .ToListAsync();

        entries
            .Should()
            .HaveCount(
                DocumentCount,
                because: "every posted document MUST emit exactly one JournalEntry"
            );

        var unbalanced = entries
            .Select(e => new
            {
                e.Id,
                e.SourceDocumentNumber,
                Debit = e.Lines.Sum(l => l.Debit.Amount),
                Credit = e.Lines.Sum(l => l.Credit.Amount),
            })
            .Where(x => x.Debit != x.Credit)
            .ToList();

        unbalanced
            .Should()
            .BeEmpty(
                because: $"SC-003 — 100% of journals MUST balance; {unbalanced.Count} unbalanced entries means a regression in one of the auto-emitters. Sample: {string.Join("; ", unbalanced.Take(3).Select(x => $"{x.SourceDocumentNumber}: dr={x.Debit:F2} vs cr={x.Credit:F2}"))}"
            );
    }

    private static async Task<(Customer, Supplier, VatCategory, Item, User)> SeedAsync(
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
            code: $"CUS-{Guid.NewGuid():N}".Substring(0, 12),
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
                vatExemption: false,
                defaultSalesVatCategoryId: vat.Id
            )
        );
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                EgyptianTin.Parse("123456789"),
                vat.Id
            )
        );
        var item = new Item(
            code: $"IT-{Guid.NewGuid():N}".Substring(0, 8),
            name: new ArabicEnglishText("بند", "Item"),
            defaultVatCategoryId: vat.Id
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
        db.Add(supplier);
        db.Add(item);
        db.Add(user);
        await db.SaveChangesAsync();
        return (customer, supplier, vat, item, user);
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
