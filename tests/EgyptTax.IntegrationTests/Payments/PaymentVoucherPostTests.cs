using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Payments;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
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
/// Phase 9 happy-path coverage for the two payment voucher post
/// handlers. Pins the GL effect (DR AP / CR Cash on supplier side;
/// DR Cash / CR AR on customer side) at Phase 9 cut where WHT
/// amounts are zero. Independent test from the Phase 9 spec
/// section: "Post a customer receipt voucher allocating 1,000 EGP
/// cash to one outstanding sales invoice; AR balance for that
/// invoice drops to zero; balanced journal voucher generated."
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PaymentVoucherPostTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Phase9_IndependentTest_CustomerReceipt_FullyClosesArOnTargetInvoice()
    {
        // Independent test from Phase 9 spec.
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedSalesAsync(db);

        // Post a 1,140 sales invoice (1000 + 140 VAT).
        var posted = await PostSalesAsync(db, customer, item, vat, user, unitPrice: 1_000m);

        // Create a receipt voucher with gross 1,140 + allocate the full amount.
        var voucher = CustomerReceiptVoucher.CreateDraft(
            customer.Id,
            new DateOnly(2026, 5, 9),
            PaymentMethod.BankTransfer,
            "RCV-1",
            MoneyEgp.From(1_140m)
        );
        db.Add(voucher);
        await db.SaveChangesAsync();

        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateCustomerReceiptCommand(voucher.Id, posted.Id, MoneyEgp.From(1_140m)),
            CancellationToken.None
        );

        // Post.
        var postHandler = new PostCustomerReceiptVoucherHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore(),
            new CustomerReceiptVoucherJournalEmitter(db)
        );
        var postedVoucher = await postHandler.HandleAsync(
            new PostCustomerReceiptVoucherCommand(voucher.Id, user.Id),
            CancellationToken.None
        );

        postedVoucher.State.Should().Be(DocumentState.Posted);
        postedVoucher
            .DocumentNumber.Should()
            .StartWith(
                "CRV-2026-",
                because: "FR-011 — sequential CRV-{year}-{n} allocated on Post"
            );

        // GL effect: 2-line balanced JE — DR Cash 1,140 / CR AR 1,140.
        db.ChangeTracker.Clear();
        var je = await db.Set<JournalEntry>()
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == postedVoucher.Id);
        je.Lines.Should()
            .HaveCount(2, because: "Phase 9 default — no WHT, so 2-line JE (Cash + AR)");
        je.Lines.Sum(l => l.Debit.Amount).Should().Be(1_140m);
        je.Lines.Sum(l => l.Credit.Amount).Should().Be(1_140m);

        var cash = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.Cash);
        var ar = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsReceivable);
        cash.Debit.Amount.Should().Be(1_140m);
        ar.Credit.Amount.Should().Be(1_140m);

        // AR open balance for the invoice = 1,140 (sales) − 1,140 (allocation) = 0.
        // (Calculation: same query the handler uses.)
        var allocated = await db.Set<PaymentAllocation>()
            .AsNoTracking()
            .Where(a => a.TargetDocumentId == posted.Id)
            .SumAsync(a => a.AllocatedAmount.Amount);
        var openBalance = posted.GrandTotal.Amount - allocated;
        openBalance
            .Should()
            .Be(0m, because: "the receipt voucher fully closes the invoice's AR balance");
    }

    [Fact]
    public async Task SupplierPaymentPost_Emits2LineJe_DrApCrCash_AtPhase9NoWhtCut()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedPurchaseAsync(db);

        // Post a 570 purchase invoice (500 + 70 VAT, non-deductible).
        var purchaseDraft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-PAY",
            new DateOnly(2026, 5, 9)
        );
        purchaseDraft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(500m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent,
            deductibleFlag: false
        );
        db.Add(purchaseDraft);
        await db.SaveChangesAsync();
        var postedPurchase = await new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        ).HandleAsync(
            new PostPurchaseInvoiceCommand(purchaseDraft.Id, user.Id),
            CancellationToken.None
        );

        // Pay it.
        var voucher = SupplierPaymentVoucher.CreateDraft(
            supplier.Id,
            new DateOnly(2026, 5, 10),
            PaymentMethod.BankTransfer,
            "PAY-1",
            MoneyEgp.From(570m)
        );
        db.Add(voucher);
        await db.SaveChangesAsync();

        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateSupplierPaymentCommand(voucher.Id, postedPurchase.Id, MoneyEgp.From(570m)),
            CancellationToken.None
        );

        var posted = await new PostSupplierPaymentVoucherHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore(),
            new SupplierPaymentVoucherJournalEmitter(db)
        ).HandleAsync(
            new PostSupplierPaymentVoucherCommand(voucher.Id, user.Id),
            CancellationToken.None
        );

        posted.State.Should().Be(DocumentState.Posted);
        posted.DocumentNumber.Should().StartWith("SPV-2026-");

        db.ChangeTracker.Clear();
        var je = await db.Set<JournalEntry>()
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);
        je.Lines.Should()
            .HaveCount(2, because: "Phase 9 — no WHT split yet; 2-line JE (AP + Cash)");

        var ap = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsPayable);
        var cash = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.Cash);
        ap.Debit.Amount.Should()
            .Be(570m, because: "settle the AP raised by the purchase invoice posting");
        cash.Credit.Amount.Should()
            .Be(570m, because: "Phase 9 — full gross goes to cash leg (no WHT withheld)");
    }

    [Fact]
    public async Task SupplierPaymentPost_With_WhtSplit_Emits3LineJe_DrApCrCashCrWhtPayable()
    {
        // Pre-validates the US7 path: when ApplyWhtSplit was called on
        // the draft voucher, the emitter MUST produce the 3-line form
        // automatically. Without this guarantee US7 has to revisit the
        // emitter. With it, US7 just adds the WhtComputeService call.
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedPurchaseAsync(db);

        var purchaseDraft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-WHT",
            new DateOnly(2026, 5, 9)
        );
        purchaseDraft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(10_000m),
            vatCategoryId: vat.Id,
            vatRatePercent: 0m,
            deductibleFlag: false
        );
        db.Add(purchaseDraft);
        await db.SaveChangesAsync();
        var postedPurchase = await new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore()
        ).HandleAsync(
            new PostPurchaseInvoiceCommand(purchaseDraft.Id, user.Id),
            CancellationToken.None
        );

        var voucher = SupplierPaymentVoucher.CreateDraft(
            supplier.Id,
            new DateOnly(2026, 5, 10),
            PaymentMethod.BankTransfer,
            "PAY-WHT",
            MoneyEgp.From(10_000m)
        );
        // Apply 5% WHT split (500 EGP withheld, 9,500 cash leg).
        voucher.ApplyWhtSplit(MoneyEgp.From(500m), Guid.NewGuid());
        db.Add(voucher);
        await db.SaveChangesAsync();

        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateSupplierPaymentCommand(
                voucher.Id,
                postedPurchase.Id,
                MoneyEgp.From(10_000m)
            ),
            CancellationToken.None
        );

        var posted = await new PostSupplierPaymentVoucherHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            new TestClock(new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore(),
            new SupplierPaymentVoucherJournalEmitter(db)
        ).HandleAsync(
            new PostSupplierPaymentVoucherCommand(voucher.Id, user.Id),
            CancellationToken.None
        );

        db.ChangeTracker.Clear();
        var je = await db.Set<JournalEntry>()
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);
        je.Lines.Should()
            .HaveCount(3, because: "WHT split → 3-line JE (DR AP / CR Cash / CR WhtPayable)");

        var ap = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsPayable);
        var cash = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.Cash);
        var wht = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.WhtPayable);
        ap.Debit.Amount.Should().Be(10_000m);
        cash.Credit.Amount.Should().Be(9_500m, because: "net cash = gross 10k − WHT 500");
        wht.Credit.Amount.Should().Be(500m);
    }

    private static async Task<SalesInvoice> PostSalesAsync(
        AppDbContext db,
        Customer customer,
        Item item,
        VatCategory vat,
        User user,
        decimal unitPrice
    )
    {
        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 9)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(unitPrice), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var emitter = new SalesInvoiceJournalEmitter(db);
        var handler = new PostSalesInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore(),
            emitter
        );
        return await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );
    }

    private static async Task<(Customer, Item, VatCategory, User)> SeedSalesAsync(AppDbContext db)
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
        db.Add(item);
        db.Add(user);
        await db.SaveChangesAsync();
        return (customer, item, vat, user);
    }

    private static async Task<(Supplier, VatCategory, User)> SeedPurchaseAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
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
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(vat);
        db.Add(supplier);
        db.Add(user);
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
