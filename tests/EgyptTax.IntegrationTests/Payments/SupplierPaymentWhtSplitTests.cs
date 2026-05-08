using EgyptTax.Application.Audit;
using EgyptTax.Application.Payments;
using EgyptTax.Application.Purchases;
using EgyptTax.Application.Wht;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Tax;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Payments;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.Infrastructure.Wht;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Payments;

/// <summary>
/// T188 / FR-051 / FR-045 / US7 — supplier-payment WHT split
/// end-to-end. Posts a supplier payment voucher with a 5% WHT
/// category in force on the payment date and asserts:
///   * The 3-line balanced JE is emitted: DR AP gross / CR Cash net /
///     CR WhtPayable wht.
///   * An outbound WhtCertificate row is created with the right
///     direction / counterparty / source-voucher / source-invoice /
///     category / rate / amount / number.
///   * The voucher's WhtPayableAmount + NetCashPaid + GeneratedWht
///     CertificateId reflect the split.
///
/// Closes the deferred test from Phase 9 batch 1 — needed
/// WhtComputeService (US7 batch 1) + voucher integration
/// (US7 batch 2) to land first.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SupplierPaymentWhtSplitTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task SupplierPayment_WithWhtSplit_Emits3LineJe_AndCreatesOutboundCertificate()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedAsync(db);

        // Seed a 5% Services WHT category effective for our payment date.
        var whtCategory = new WhtCategory(
            code: "Services",
            name: new ArabicEnglishText("خدمات", "Services"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices
        );
        db.Add(whtCategory);

        // Post a 10,000 EGP purchase invoice (no VAT to keep maths clean).
        var purchaseDraft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-WHT-T188",
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

        // Pay it with WHT split applied.
        var voucher = SupplierPaymentVoucher.CreateDraft(
            supplier.Id,
            new DateOnly(2026, 5, 10),
            PaymentMethod.BankTransfer,
            "PAY-T188",
            MoneyEgp.From(10_000m)
        );
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
            new SupplierPaymentVoucherJournalEmitter(db),
            new SqlWhtComputeService(db)
        ).HandleAsync(
            new PostSupplierPaymentVoucherCommand(
                voucher.Id,
                user.Id,
                WhtCategoryCode: "Services",
                WhtSourceInvoiceId: postedPurchase.Id
            ),
            CancellationToken.None
        );

        // Voucher state.
        posted.WhtPayableAmount.Amount.Should().Be(500m, because: "10k × 5% = 500");
        posted.NetCashPaid.Amount.Should().Be(9_500m);
        posted.GeneratedWhtCertificateId.Should().NotBeNull();

        // Outbound certificate persisted.
        db.ChangeTracker.Clear();
        var cert = await db.Set<WhtCertificate>()
            .AsNoTracking()
            .FirstAsync(c => c.SourceVoucherId == posted.Id);
        cert.Direction.Should().Be(WhtCertificateDirection.OutboundToSupplier);
        cert.CounterpartyId.Should().Be(supplier.Id);
        cert.SourceInvoiceId.Should().Be(postedPurchase.Id);
        cert.WhtCategoryId.Should().Be(whtCategory.Id);
        cert.RateAppliedPercent.Should().Be(5m);
        cert.AmountWithheld.Amount.Should().Be(500m);
        cert.CertificateNumber.Should()
            .StartWith(
                "WHT-SPV-2026-",
                because: "outbound cert numbers are derived from the voucher's SPV-{year}-{n} document number"
            );

        // 3-line balanced JE.
        var je = await db.Set<JournalEntry>()
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);
        je.Lines.Should().HaveCount(3);
        je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsPayable)
            .Debit.Amount.Should()
            .Be(10_000m);
        je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.Cash)
            .Credit.Amount.Should()
            .Be(9_500m);
        je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.WhtPayable)
            .Credit.Amount.Should()
            .Be(500m);
    }

    [Fact]
    public async Task SupplierPayment_WhtCategoryNotEffective_OnPaymentDate_IsRefused()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedAsync(db);

        // Seed a category effective starting 2027 (after our payment date).
        var whtCategory = new WhtCategory(
            code: "Services-2027",
            name: new ArabicEnglishText("خدمات", "Services 2027"),
            ratePercent: 5m,
            effectiveFromDate: new DateOnly(2027, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices
        );
        db.Add(whtCategory);

        var purchaseDraft = PurchaseInvoice.CreateDraft(
            supplier.Id,
            supplier.TaxProfile,
            "SUP-INV-NEFF",
            new DateOnly(2026, 5, 9)
        );
        purchaseDraft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(1_000m),
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
            "PAY-NEFF",
            MoneyEgp.From(1_000m)
        );
        db.Add(voucher);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateSupplierPaymentCommand(
                voucher.Id,
                postedPurchase.Id,
                MoneyEgp.From(1_000m)
            ),
            CancellationToken.None
        );

        var act = async () =>
            await new PostSupplierPaymentVoucherHandler(
                db,
                new SqlSequentialNumberAllocator(db),
                new TestClock(new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc)),
                new CaptureAuditLogStore(),
                new SupplierPaymentVoucherJournalEmitter(db),
                new SqlWhtComputeService(db)
            ).HandleAsync(
                new PostSupplierPaymentVoucherCommand(
                    voucher.Id,
                    user.Id,
                    WhtCategoryCode: "Services-2027",
                    WhtSourceInvoiceId: postedPurchase.Id
                ),
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("not effective", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(Supplier, VatCategory, User)> SeedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 0m,
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
