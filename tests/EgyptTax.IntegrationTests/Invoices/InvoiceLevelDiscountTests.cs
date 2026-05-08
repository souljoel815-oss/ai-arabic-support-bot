using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Invoices;

/// <summary>
/// T082 — FR-008 expansion: header-level invoice discount (% or fixed
/// amount) MUST apportion across lines pro-rata so each line's VAT is
/// computed against its post-discount net subtotal. The header-level
/// totals are the sum of the apportioned line totals; the line-level
/// LineApportionedDiscount values MUST sum exactly to the
/// invoice-level discount amount (no banker's-rounding leakage). The
/// test exercises percent and fixed-amount paths, mixed-VAT-rate
/// apportionment (where a zero-rated line absorbs apportioned discount
/// without contributing VAT), validation rules, and the full posting
/// flow with a discount applied.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class InvoiceLevelDiscountTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task PercentDiscount_OnSingleLine_RecomputesNetVatAndGrandTotal()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);

        var invoice = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);

        invoice.SetInvoiceLevelDiscount(amount: null, percent: 10m);

        invoice
            .Subtotal.Amount.Should()
            .Be(
                1_000m,
                because: "the subtotal column carries pre-discount line-subtotal sum so audit can see the gross figure"
            );
        invoice.InvoiceLevelDiscountAmount.Amount.Should().Be(100m, because: "10% of 1000 = 100");
        invoice.NetBeforeVat.Amount.Should().Be(900m);
        invoice
            .VatTotal.Amount.Should()
            .Be(126m, because: "VAT recomputes against the discounted subtotal: 900 × 14% = 126");
        invoice.GrandTotal.Amount.Should().Be(1_026m);

        var line = invoice.Lines.Single();
        line.LineSubtotal.Amount.Should()
            .Be(1_000m, because: "line carries the pre-discount subtotal");
        line.LineApportionedDiscount.Amount.Should()
            .Be(100m, because: "with one line, all of the invoice-level discount apportions to it");
        line.LineNetSubtotal.Amount.Should().Be(900m);
        line.LineVat.Amount.Should().Be(126m);
        line.LineTotal.Amount.Should().Be(1_026m);
    }

    [Fact]
    public async Task FixedAmountDiscount_MatchesEquivalentPercent()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);

        var invoice = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);

        invoice.SetInvoiceLevelDiscount(amount: MoneyEgp.From(100m), percent: null);

        invoice.NetBeforeVat.Amount.Should().Be(900m);
        invoice.VatTotal.Amount.Should().Be(126m);
        invoice.GrandTotal.Amount.Should().Be(1_026m);
        invoice.InvoiceLevelDiscountAmount.Amount.Should().Be(100m);
        invoice
            .InvoiceLevelDiscountPercent.Should()
            .Be(
                0m,
                because: "the fixed-amount path leaves the percent flag at 0 — exactly one of {amount, percent} carries the truth"
            );
    }

    [Fact]
    public async Task MixedRateLines_ApportionDiscount_ProRata()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, standardVat) = await SeedMasterDataAsync(db);

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

        var invoice = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(600m), standardVat.Id, standardVat.RatePercent);
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(400m), zeroVat.Id, zeroVat.RatePercent);

        invoice.SetInvoiceLevelDiscount(amount: null, percent: 10m);

        // Pre-discount subtotal: 600 + 400 = 1000.
        // 10% header discount: 100 EGP, apportioned 60 to line A (600/1000) and 40 to line B (400/1000).
        // Line A: net 540, VAT 540 × 14% = 75.6, total 615.6
        // Line B: net 360, VAT 0, total 360
        // Header: subtotal 1000, NetBeforeVat 900, VAT 75.6, grand total 975.6
        invoice.Subtotal.Amount.Should().Be(1_000m);
        invoice.NetBeforeVat.Amount.Should().Be(900m);
        invoice.VatTotal.Amount.Should().Be(75.60m);
        invoice.GrandTotal.Amount.Should().Be(975.60m);

        var standardLine = invoice.Lines.First();
        standardLine.LineApportionedDiscount.Amount.Should().Be(60m);
        standardLine.LineNetSubtotal.Amount.Should().Be(540m);
        standardLine.LineVat.Amount.Should().Be(75.60m);

        var zeroLine = invoice.Lines.Skip(1).First();
        zeroLine.LineApportionedDiscount.Amount.Should().Be(40m);
        zeroLine.LineNetSubtotal.Amount.Should().Be(360m);
        zeroLine
            .LineVat.Amount.Should()
            .Be(
                0m,
                because: "a zero-rated line absorbs its apportioned discount but contributes no VAT"
            );
    }

    [Fact]
    public async Task ApportionedDiscounts_SumExactly_ToInvoiceLevelDiscount()
    {
        // Three uneven lines + a percentage that exercises rounding —
        // the last-line residue policy MUST keep sum(apportioned) ==
        // invoice-level discount to the cent. This is the FR-008
        // invariant the audit chain relies on.
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);

        var invoice = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(333.33m), vat.Id, vat.RatePercent);
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(333.33m), vat.Id, vat.RatePercent);
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(333.34m), vat.Id, vat.RatePercent);
        invoice.SetInvoiceLevelDiscount(amount: null, percent: 7m);

        var sumOfApportioned = invoice.Lines.Sum(l => l.LineApportionedDiscount.Amount);
        sumOfApportioned
            .Should()
            .Be(
                invoice.InvoiceLevelDiscountAmount.Amount,
                because: "FR-008 invariant — apportioned line discounts MUST sum exactly to the invoice-level discount; the rounding remainder lands on the last line"
            );
    }

    [Fact]
    public async Task SetDiscount_BothAmountAndPercent_Throws()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        var invoice = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(100m), vat.Id, vat.RatePercent);

        var act = () => invoice.SetInvoiceLevelDiscount(amount: MoneyEgp.From(10m), percent: 10m);
        act.Should()
            .Throw<ArgumentException>(
                because: "exactly one of {amount, percent} carries the discount; both is ambiguous"
            );
    }

    [Fact]
    public async Task SetDiscount_PercentOver100_Throws()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        var invoice = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(100m), vat.Id, vat.RatePercent);

        var act = () => invoice.SetInvoiceLevelDiscount(amount: null, percent: 101m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SetDiscount_AmountAboveSubtotal_Throws()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        var invoice = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(100m), vat.Id, vat.RatePercent);

        var act = () => invoice.SetInvoiceLevelDiscount(amount: MoneyEgp.From(200m), percent: null);
        act.Should()
            .Throw<InvalidOperationException>(
                because: "discount amount cannot exceed the pre-discount subtotal — that would be a credit, not a discount"
            );
    }

    [Fact]
    public async Task PostingFlow_PreservesInvoiceLevelDiscount_OnRoundTrip()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat) = await SeedMasterDataAsync(db);
        var operatorUser = await SeedOperatorUserAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 2m, MoneyEgp.From(500m), vat.Id, vat.RatePercent);
        draft.SetInvoiceLevelDiscount(amount: null, percent: 10m);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var allocator = new SqlSequentialNumberAllocator(db);
        var auditCapture = new CaptureAuditLogStore();
        var handler = new PostSalesInvoiceHandler(db, allocator, clock, auditCapture);

        var posted = await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );

        // 2 × 500 = 1000 subtotal; 10% discount = 100; net 900; VAT 126; grand 1026.
        posted.Subtotal.Amount.Should().Be(1_000m);
        posted.InvoiceLevelDiscountPercent.Should().Be(10m);
        posted.InvoiceLevelDiscountAmount.Amount.Should().Be(100m);
        posted.NetBeforeVat.Amount.Should().Be(900m);
        posted.VatTotal.Amount.Should().Be(126m);
        posted.GrandTotal.Amount.Should().Be(1_026m);

        // Re-load: discount columns MUST round-trip through the EF
        // mapping — catches a regression where the migration columns
        // weren't bound or the configurator dropped a property.
        db.ChangeTracker.Clear();
        var reloaded = await db.Set<SalesInvoice>()
            .Include(i => i.Lines)
            .FirstAsync(i => i.Id == draft.Id);
        reloaded.InvoiceLevelDiscountPercent.Should().Be(10m);
        reloaded.InvoiceLevelDiscountAmount.Amount.Should().Be(100m);
        reloaded.NetBeforeVat.Amount.Should().Be(900m);
        reloaded.GrandTotal.Amount.Should().Be(1_026m);
        reloaded.Lines.Single().LineApportionedDiscount.Amount.Should().Be(100m);
        reloaded.Lines.Single().LineNetSubtotal.Amount.Should().Be(900m);
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
