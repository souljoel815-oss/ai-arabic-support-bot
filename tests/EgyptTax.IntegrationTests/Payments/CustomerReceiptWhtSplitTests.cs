using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Payments;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Tax;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Payments;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Wht;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Payments;

/// <summary>
/// T189 / FR-052 / FR-045 / US7 — customer-receipt WHT split
/// end-to-end. Customer issues a WHT certificate; we record the
/// inbound certificate (customer's number is authoritative) and
/// the JE picks up the 3-line shape: DR Cash net + DR
/// WhtReceivable + CR AR gross.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class CustomerReceiptWhtSplitTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task CustomerReceipt_WithCustomerWhtCertificate_Emits3LineJe_AndRecordsInboundCertificate()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedAsync(db);

        // Customer-side category effective for our receipt date.
        var whtCategory = new WhtCategory(
            code: "Cust-Services",
            name: new ArabicEnglishText("خدمات", "Customer-side services"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.CustomersServices);
        db.Add(whtCategory);

        // Post a sales invoice (1,000 net + 14% VAT = 1,140 gross).
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile,
            new DateOnly(2026, 5, 9));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();
        var postedSales = await new PostSalesInvoiceHandler(db,
                new SqlSequentialNumberAllocator(db),
                new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
                new CaptureAuditLogStore(),
                new SalesInvoiceJournalEmitter(db))
            .HandleAsync(new PostSalesInvoiceCommand(draft.Id, user.Id),
                CancellationToken.None);

        // Customer pays the 1,140 gross + provides a WHT certificate
        // for 57 (5% of 1,140). Cash received = 1,083.
        var voucher = CustomerReceiptVoucher.CreateDraft(customer.Id,
            new DateOnly(2026, 5, 10), PaymentMethod.BankTransfer, "RCV-WHT-T189",
            MoneyEgp.From(1_140m));
        db.Add(voucher);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(voucher.Id, postedSales.Id, MoneyEgp.From(1_140m)),
            CancellationToken.None);

        var posted = await new PostCustomerReceiptVoucherHandler(db,
                new SqlSequentialNumberAllocator(db),
                new TestClock(new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc)),
                new CaptureAuditLogStore(),
                new CustomerReceiptVoucherJournalEmitter(db),
                new SqlWhtComputeService(db))
            .HandleAsync(new PostCustomerReceiptVoucherCommand(
                voucher.Id, user.Id,
                CustomerWhtCertificateNumber: "CUST-CERT-2026-001",
                CustomerWhtAmount: 57m,
                WhtCategoryCode: "Cust-Services",
                WhtSourceInvoiceId: postedSales.Id),
                CancellationToken.None);

        // Voucher state.
        posted.WhtReceivableAmount.Amount.Should().Be(57m,
            because: "customer's number is authoritative — 57 withheld");
        posted.NetCashReceived.Amount.Should().Be(1_083m,
            because: "1140 gross − 57 customer-withheld = 1083 cash received");
        posted.CustomerWhtCertificateId.Should().NotBeNull();

        // Inbound certificate persisted with the customer-supplied number.
        db.ChangeTracker.Clear();
        var cert = await db.Set<WhtCertificate>().AsNoTracking()
            .FirstAsync(c => c.SourceVoucherId == posted.Id);
        cert.Direction.Should().Be(WhtCertificateDirection.InboundFromCustomer);
        cert.CounterpartyId.Should().Be(customer.Id);
        cert.SourceInvoiceId.Should().Be(postedSales.Id);
        cert.WhtCategoryId.Should().Be(whtCategory.Id);
        cert.AmountWithheld.Amount.Should().Be(57m);
        cert.CertificateNumber.Should().Be("CUST-CERT-2026-001",
            because: "the customer's certificate number is preserved verbatim — it's their record we're filing");

        // 3-line balanced JE.
        var je = await db.Set<JournalEntry>().AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);
        je.Lines.Should().HaveCount(3);
        je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.Cash)
            .Debit.Amount.Should().Be(1_083m);
        je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.WhtReceivable)
            .Debit.Amount.Should().Be(57m);
        je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsReceivable)
            .Credit.Amount.Should().Be(1_140m);
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
