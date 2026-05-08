using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Payments;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Payments;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Payments;

/// <summary>
/// T190 / FR-053 — payment-allocation MUST refuse over-allocation
/// across both axes:
///   * Voucher cap: SUM(allocations on this voucher) ≤ gross.
///     Aggregate-level invariant — repeated here end-to-end so a
///     future refactor that bypasses the aggregate (e.g., direct
///     SQL inserts in seeds) still gets caught.
///   * Per-target invoice cap: a single allocation MUST NOT exceed
///     the target invoice's open balance, where open_balance =
///     grand_total − SUM(existing PaymentAllocation against it
///     across ALL vouchers). Handler-side because it requires a
///     DB lookup; pinned here so a regression that drops the
///     check is caught.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class OverAllocationGuardTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task AllocateMoreThanInvoiceOpenBalance_IsRefused()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedSalesAsync(db);

        // Post a 1,140 sales invoice (1000 + 14% VAT).
        var posted = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);
        // Open balance = 1,140 (no prior allocations).

        // Create a receipt voucher with gross 1,500 and try to
        // allocate 1,500 to the invoice → exceeds the 1,140 open
        // balance.
        var voucher = CustomerReceiptVoucher.CreateDraft(
            customer.Id, new DateOnly(2026, 5, 9),
            PaymentMethod.BankTransfer, "RCV-OVER",
            MoneyEgp.From(1_500m));
        db.Add(voucher);
        await db.SaveChangesAsync();

        var allocateHandler = new AllocatePaymentHandler(db);
        var act = async () => await allocateHandler.HandleAsync(
            new AllocateCustomerReceiptCommand(
                CustomerReceiptVoucherId: voucher.Id,
                TargetSalesInvoiceId: posted.Id,
                AllocatedAmount: MoneyEgp.From(1_500m)),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-053", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("open balance", StringComparison.OrdinalIgnoreCase));

        // No allocation row persisted.
        var count = await db.Set<PaymentAllocation>().AsNoTracking()
            .CountAsync(a => a.CustomerReceiptVoucherId == voucher.Id);
        count.Should().Be(0,
            because: "the rejection MUST happen BEFORE the allocation lands");
    }

    [Fact]
    public async Task AllocationsAcrossTwoVouchers_RespectInvoiceOpenBalance()
    {
        // First voucher allocates 1,000 against a 1,140 invoice
        // (open balance after = 140). Second voucher trying to
        // allocate 200 MUST be refused — only 140 remains open.
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedSalesAsync(db);
        var posted = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);

        var v1 = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 9), PaymentMethod.Cash, "RCV-1",
            MoneyEgp.From(1_000m));
        db.Add(v1);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(v1.Id, posted.Id, MoneyEgp.From(1_000m)),
            CancellationToken.None);

        // Second voucher tries 200 — only 140 of open balance remains.
        var v2 = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 10), PaymentMethod.Cash, "RCV-2",
            MoneyEgp.From(500m));
        db.Add(v2);
        await db.SaveChangesAsync();

        var act = async () => await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(v2.Id, posted.Id, MoneyEgp.From(200m)),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("open balance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SupplierPaymentAllocation_RefusedAgainstNonPostedInvoice()
    {
        // Allocating against a Draft invoice is non-sensical — it
        // has no open balance yet. Handler refuses before even
        // computing the open-balance math.
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedPurchaseAsync(db);

        var draftInvoice = PurchaseInvoice.CreateDraft(supplier.Id, supplier.TaxProfile,
            "SUP-DRAFT", new DateOnly(2026, 5, 9));
        draftInvoice.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(500m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: false);
        db.Add(draftInvoice);

        var voucher = SupplierPaymentVoucher.CreateDraft(
            supplier.Id, new DateOnly(2026, 5, 9),
            PaymentMethod.BankTransfer, "PAY-X",
            MoneyEgp.From(500m));
        db.Add(voucher);
        await db.SaveChangesAsync();

        var act = async () => await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateSupplierPaymentCommand(voucher.Id, draftInvoice.Id, MoneyEgp.From(100m)),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("not Posted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VoucherCap_Refuses_AllocationsExceedingGross()
    {
        // The voucher-cap check lives in the aggregate's AddAllocation
        // (FR-053 voucher cap). Already covered by unit tests, but
        // pinned here end-to-end so a regression in the handler
        // wrapping (e.g., bypassing AddAllocation) is also caught.
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedSalesAsync(db);

        // Two posted invoices so we have headroom on both targets.
        var inv1 = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);
        var inv2 = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);

        var voucher = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 9), PaymentMethod.Cash, "RCV-CAP",
            MoneyEgp.From(1_000m));
        db.Add(voucher);
        await db.SaveChangesAsync();

        // First 800 OK.
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(voucher.Id, inv1.Id, MoneyEgp.From(800m)),
            CancellationToken.None);

        // Second 300 would push voucher total to 1,100 > gross 1,000.
        var act = async () => await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(voucher.Id, inv2.Id, MoneyEgp.From(300m)),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("FR-053", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("voucher cap", StringComparison.OrdinalIgnoreCase));
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

    private static async Task<(Supplier, VatCategory, User)> SeedPurchaseAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("123456789"), vat.Id));
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(supplier); db.Add(user);
        await db.SaveChangesAsync();
        return (supplier, vat, user);
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
