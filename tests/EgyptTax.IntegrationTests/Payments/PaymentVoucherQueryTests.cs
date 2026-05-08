using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Payments;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Payments;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Payments;

/// <summary>
/// T195 — read surface behind the payment-voucher Razor pages.
/// Pins the operator-visible numbers: outstanding invoices show
/// only those with open_balance > 0, the per-invoice allocation
/// view rolls up multiple vouchers' allocations into one history
/// + computes open_balance the same way the
/// AllocatePaymentHandler does.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PaymentVoucherQueryTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task ListOutstandingSalesInvoices_ShowsOnlyInvoicesWithOpenBalance()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedSalesAsync(db);

        // Two invoices: one fully settled, one partially allocated.
        var paid = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);
        var partial = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);

        // Voucher A: fully settles "paid" (1140 against 1140).
        var vA = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 9), PaymentMethod.Cash, "RCV-A",
            MoneyEgp.From(1_140m));
        db.Add(vA);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(vA.Id, paid.Id, MoneyEgp.From(1_140m)),
            CancellationToken.None);

        // Voucher B: partial 500 against "partial" (640 still open).
        var vB = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 10), PaymentMethod.Cash, "RCV-B",
            MoneyEgp.From(500m));
        db.Add(vB);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(vB.Id, partial.Id, MoneyEgp.From(500m)),
            CancellationToken.None);

        var query = new SqlPaymentVoucherQuery(db);
        var outstanding = await query.ListOutstandingSalesInvoicesAsync(customer.Id);

        outstanding.Should().HaveCount(1,
            because: "the fully-settled invoice MUST drop off the outstanding list");
        outstanding[0].InvoiceId.Should().Be(partial.Id);
        outstanding[0].AllocatedToDate.Amount.Should().Be(500m);
        outstanding[0].OpenBalance.Amount.Should().Be(640m,
            because: "1140 grand total − 500 allocated = 640 open balance");
    }

    [Fact]
    public async Task GetInvoiceAllocations_RollsUpMultiVoucherHistory_AndExposesOpenBalance()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedSalesAsync(db);
        var posted = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);

        // Two vouchers, total 800 + 200 = 1000 against the 1140 invoice.
        var v1 = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 9), PaymentMethod.Cash, "RCV-V1",
            MoneyEgp.From(800m));
        db.Add(v1);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(v1.Id, posted.Id, MoneyEgp.From(800m)),
            CancellationToken.None);

        var v2 = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 10), PaymentMethod.Cash, "RCV-V2",
            MoneyEgp.From(200m));
        db.Add(v2);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(v2.Id, posted.Id, MoneyEgp.From(200m)),
            CancellationToken.None);

        var view = await new SqlPaymentVoucherQuery(db)
            .GetInvoiceAllocationsAsync(posted.Id, DocumentType.SalesInvoice);

        view.Should().NotBeNull();
        view!.AllocatedToDate.Amount.Should().Be(1_000m);
        view.OpenBalance.Amount.Should().Be(140m,
            because: "1140 grand total − 1000 allocated = 140 open balance");
        view.Allocations.Should().HaveCount(2);
        view.Allocations.Sum(a => a.AllocatedAmount.Amount).Should().Be(1_000m);
    }

    private static async Task<SalesInvoice> PostSalesAsync(
        AppDbContext db, Customer customer, Item item, VatCategory vat, User user, decimal unitPrice)
    {
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, new DateOnly(2026, 5, 9));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(unitPrice), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var emitter = new SalesInvoiceJournalEmitter(db);
        var handler = new PostSalesInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), clock,
            new CaptureAuditLogStore(), emitter);
        return await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, user.Id), CancellationToken.None);
    }

    private static async Task<(Customer, Item, VatCategory, User)> SeedSalesAsync(AppDbContext db)
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
