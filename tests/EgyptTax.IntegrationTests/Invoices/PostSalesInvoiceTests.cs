using EgyptTax.Application.Audit;
using EgyptTax.Application.Identity;
using EgyptTax.Application.Invoices;
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
/// T080 — US1 acceptance scenario 1: post a sales invoice for 1,000 EGP
/// at 14 % VAT and observe (a) computed totals 1,000 / 140 / 1,140,
/// (b) state transitioning Draft → Posted directly per the Phase-1
/// approval-disabled default, (c) document number allocated as
/// <c>INV-{YYYY}-000001</c>, (d) FR-026 audit entry recorded with
/// <c>posting_mode = UnapprovedDirect</c>. Drives the full path the
/// US1 UI will exercise on click of "Post" so configuration drift
/// (numbering allocator binding, approval-setting lookup, audit
/// emission) surfaces at the smallest possible test boundary.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PostSalesInvoiceTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Post_OneLine_1000Egp_At14Percent_ComputesTotals_AndTransitionsToPosted()
    {
        await using var db = await _fixture.CreateContextAsync();

        var (customer, item, vat) = await SeedMasterDataAsync(db);
        var operatorUser = await SeedOperatorUserAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customerId: customer.Id,
            customerTaxProfileSnapshot: customer.TaxProfile,
            documentDate: new DateOnly(2026, 5, 7)
        );
        draft.AddLine(
            itemId: item.Id,
            quantity: 1m,
            unitPrice: MoneyEgp.From(1_000m),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent
        );
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

        posted
            .State.Should()
            .Be(
                DocumentState.Posted,
                because: "FR-026 — sales invoices have approval disabled by Phase-1 default; Draft→Posted directly is allowed"
            );
        posted.PostingMode.Should().Be(DocumentPostingMode.UnapprovedDirect);
        posted.PostedByUserId.Should().Be(operatorUser.Id);
        posted.PostedAtUtc.Should().Be(clock.UtcNow);
        posted.DocumentNumber.Should().Be("INV-2026-000001");

        posted.Subtotal.Amount.Should().Be(1_000m);
        posted.VatTotal.Amount.Should().Be(140m);
        posted.GrandTotal.Amount.Should().Be(1_140m);

        posted.Lines.Should().HaveCount(1);
        var line = posted.Lines.Single();
        line.LineSubtotal.Amount.Should().Be(1_000m);
        line.LineVat.Amount.Should().Be(140m);
        line.LineTotal.Amount.Should().Be(1_140m);

        auditCapture.Captured.Should().ContainSingle(e => e.Kind == "sales_invoice.posted");
        var audit = auditCapture.Captured.Single(e => e.Kind == "sales_invoice.posted");
        audit
            .PayloadJson.Should()
            .Contain(
                "UnapprovedDirect",
                because: "the posting_mode discriminator MUST be in the audit payload per FR-026"
            );
        audit.PayloadJson.Should().Contain("INV-2026-000001");
    }

    [Fact]
    public async Task Post_TwoLines_DifferentVatRates_AggregatesTotalsCorrectly()
    {
        await using var db = await _fixture.CreateContextAsync();

        var (customer, item, standardVat) = await SeedMasterDataAsync(db);
        var operatorUser = await SeedOperatorUserAsync(db);

        var zeroRated = new VatCategory(
            code: "ZeroRated",
            name: new ArabicEnglishText("صفر", "Zero rated"),
            ratePercent: 0m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        db.Add(zeroRated);
        await db.SaveChangesAsync();

        var draft = SalesInvoice.CreateDraft(
            customerId: customer.Id,
            customerTaxProfileSnapshot: customer.TaxProfile,
            documentDate: new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 2m, MoneyEgp.From(500m), standardVat.Id, standardVat.RatePercent);
        draft.AddLine(item.Id, 1m, MoneyEgp.From(200m), zeroRated.Id, zeroRated.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 0, 0, 0, DateTimeKind.Utc));
        var handler = new PostSalesInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore()
        );

        var posted = await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, operatorUser.Id),
            CancellationToken.None
        );

        // Standard line: 1000 sub, 140 vat, 1140 total
        // Zero line:    200 sub,   0 vat,  200 total
        posted.Subtotal.Amount.Should().Be(1_200m);
        posted.VatTotal.Amount.Should().Be(140m);
        posted.GrandTotal.Amount.Should().Be(1_340m);
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
        public List<EgyptTax.Domain.Audit.AuditLogPayload> Captured { get; } = [];

        public Task<EgyptTax.Domain.Audit.AuditLogEntry> AppendAsync(
            EgyptTax.Domain.Audit.AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            Captured.Add(payload);
            var entry = new EgyptTax.Domain.Audit.AuditLogEntry(
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
