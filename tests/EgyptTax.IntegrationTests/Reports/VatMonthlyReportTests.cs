using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Purchases;
using EgyptTax.Application.Reports;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.Infrastructure.Reports;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Reports;

/// <summary>
/// T126 / FR-021 — US2 acceptance scenario 1: post a 10,000 EGP
/// sale + a 4,000 EGP deductible purchase (both at 14%) and verify
/// the monthly VAT report computes net payable = 840 EGP
/// (output 1,400 - input 560). This is the most-checked report at
/// month-end; the test covers the canonical case + a couple of
/// boundary conditions.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class VatMonthlyReportTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task US2_Scenario1_Sales10k_Plus_Purchase4k_NetPayable_Is_840()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, supplier, item, vat, user) = await SeedAsync(db);

        // Sale: 10,000 net + 1,400 VAT.
        await PostSalesInvoiceAsync(
            db,
            customer,
            item,
            vat,
            user,
            documentDate: new DateOnly(2026, 5, 15),
            unitPriceEgp: 10_000m
        );

        // Deductible purchase: 4,000 net + 560 VAT against a
        // RegisteredTaxpayer supplier (so VAT is recoverable).
        await PostPurchaseInvoiceAsync(
            db,
            supplier,
            vat,
            user,
            dateReceived: new DateOnly(2026, 5, 20),
            unitPriceEgp: 4_000m,
            deductible: true,
            supplierInvoiceNumber: "SUP-001"
        );

        var query = new SqlVatMonthlyReportQuery(db);
        var report = await query.RunAsync(2026, 5);

        report
            .OutputVat.Amount.Should()
            .Be(1_400m, because: "10,000 EGP at 14% = 1,400 EGP output VAT");
        report
            .InputVatRecoverable.Amount.Should()
            .Be(
                560m,
                because: "4,000 EGP at 14% = 560 EGP input VAT (supplier is RegisteredTaxpayer so it's recoverable)"
            );
        report
            .NetPayable.Amount.Should()
            .Be(840m, because: "1,400 - 560 = 840 EGP owed to ETA per US2 acceptance scenario 1");

        report.Rows.Should().HaveCount(2);
        report.Rows.Should().Contain(r => r.ContributesToOutput && r.VatAmount.Amount == 1_400m);
        report.Rows.Should().Contain(r => !r.ContributesToOutput && r.VatAmount.Amount == 560m);
    }

    [Fact]
    public async Task PurchaseFrom_UnregisteredSupplier_DoesNot_Contribute_To_InputVat()
    {
        // FR-020 — input VAT is recoverable only against registered
        // taxpayers. A deductible purchase from an Unregistered
        // supplier MUST NOT reduce the net VAT payable; the VAT line
        // amount becomes a non-recoverable cost (booked elsewhere).
        await using var db = await _fixture.CreateContextAsync();
        var (customer, _, item, vat, user) = await SeedAsync(db);
        var unregistered = new Supplier(
            code: $"UNREG-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("غير مسجل", "Unregistered Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.Unregistered(vat.Id)
        );
        db.Add(unregistered);
        await db.SaveChangesAsync();

        await PostSalesInvoiceAsync(
            db,
            customer,
            item,
            vat,
            user,
            documentDate: new DateOnly(2026, 6, 10),
            unitPriceEgp: 10_000m
        );
        await PostPurchaseInvoiceAsync(
            db,
            unregistered,
            vat,
            user,
            dateReceived: new DateOnly(2026, 6, 12),
            unitPriceEgp: 5_000m,
            deductible: true,
            supplierInvoiceNumber: "UNREG-001"
        );

        var report = await new SqlVatMonthlyReportQuery(db).RunAsync(2026, 6);

        report.OutputVat.Amount.Should().Be(1_400m);
        report
            .InputVatRecoverable.Amount.Should()
            .Be(
                0m,
                because: "the unregistered supplier's VAT is non-recoverable per FR-020 / INV-011"
            );
        report.NetPayable.Amount.Should().Be(1_400m);
    }

    [Fact]
    public async Task NonDeductible_PurchaseLines_DoNot_Contribute_To_InputVat()
    {
        // Operator may have flipped the deductible flag off (per FR-015
        // override). Those lines MUST NOT show up in the input-VAT total.
        await using var db = await _fixture.CreateContextAsync();
        var (customer, supplier, item, vat, user) = await SeedAsync(db);

        await PostSalesInvoiceAsync(
            db,
            customer,
            item,
            vat,
            user,
            documentDate: new DateOnly(2026, 7, 1),
            unitPriceEgp: 1_000m
        );
        await PostPurchaseInvoiceAsync(
            db,
            supplier,
            vat,
            user,
            dateReceived: new DateOnly(2026, 7, 2),
            unitPriceEgp: 500m,
            deductible: false,
            supplierInvoiceNumber: "SUP-NONDED"
        );

        var report = await new SqlVatMonthlyReportQuery(db).RunAsync(2026, 7);

        report.OutputVat.Amount.Should().Be(140m);
        report
            .InputVatRecoverable.Amount.Should()
            .Be(
                0m,
                because: "the purchase line was flipped non-deductible — its VAT does NOT recover"
            );
        report.NetPayable.Amount.Should().Be(140m);
    }

    [Fact]
    public async Task DocumentsOutsidePeriod_Are_NotIncluded()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, _, item, vat, user) = await SeedAsync(db);

        // Sale in April; query for May → must not appear.
        await PostSalesInvoiceAsync(
            db,
            customer,
            item,
            vat,
            user,
            documentDate: new DateOnly(2026, 4, 30),
            unitPriceEgp: 1_000m
        );

        var report = await new SqlVatMonthlyReportQuery(db).RunAsync(2026, 5);

        report.Rows.Should().BeEmpty();
        report.OutputVat.Amount.Should().Be(0m);
        report.NetPayable.Amount.Should().Be(0m);
    }

    private static async Task PostSalesInvoiceAsync(
        AppDbContext db,
        Customer customer,
        Item item,
        VatCategory vat,
        User user,
        DateOnly documentDate,
        decimal unitPriceEgp
    )
    {
        var draft = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, documentDate);
        draft.AddLine(item.Id, 1m, MoneyEgp.From(unitPriceEgp), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(documentDate.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
        var handler = new PostSalesInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore()
        );
        await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );
    }

    private static async Task PostPurchaseInvoiceAsync(
        AppDbContext db,
        Supplier supplier,
        VatCategory vat,
        User user,
        DateOnly dateReceived,
        decimal unitPriceEgp,
        bool deductible,
        string supplierInvoiceNumber
    )
    {
        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            supplierInvoiceNumber,
            dateReceived
        );
        draft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(unitPriceEgp),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent,
            deductibleFlag: deductible
        );
        db.Add(draft);

        if (deductible)
        {
            // FR-016 — deductible purchase requires an attachment.
            db.Add(
                new Attachment(
                    documentId: draft.Id,
                    documentType: DocumentType.PurchaseInvoice,
                    filenameOriginal: "receipt.pdf",
                    filenameStorage: $"{Guid.NewGuid():N}.pdf",
                    relativePath: $"attachments/2026/{dateReceived.Month:D2}/{draft.Id:D}/receipt.pdf",
                    sha256: new byte[32],
                    mimeType: "application/pdf",
                    sizeBytes: 1234,
                    uploadedByUserId: user.Id,
                    uploadedAtUtc: dateReceived.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime()
                )
            );
        }
        await db.SaveChangesAsync();

        var clock = new TestClock(dateReceived.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime());
        var handler = new PostPurchaseInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore()
        );
        await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );
    }

    private static async Task<(
        Customer customer,
        Supplier supplier,
        Item item,
        VatCategory vat,
        User user
    )> SeedAsync(AppDbContext db)
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
            code: $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
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
            code: $"ITEM-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("صنف", "Item"),
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
        return (customer, supplier, item, vat, user);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public Task<AuditLogEntry> AppendAsync(
            AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            return Task.FromResult(
                new AuditLogEntry(
                    index: 1,
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
