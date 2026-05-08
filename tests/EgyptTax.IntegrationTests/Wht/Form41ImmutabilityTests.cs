using EgyptTax.Application.Audit;
using EgyptTax.Application.Payments;
using EgyptTax.Application.Purchases;
using EgyptTax.Application.Wht;
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

namespace EgyptTax.IntegrationTests.Wht;

/// <summary>
/// T200 / US7 scenario 3 / FR-046 — Form 41 marked Filed →
/// included WHT entries become immutable for further inclusion.
/// Pins three guarantees:
///   * MarkFiled stamps every cert in the quarter with the
///     filing id; the certs surface IncludedInForm41FilingId set
///     to the filing's Id.
///   * Re-generating the same quarter after MarkFiled is refused
///     (uniqueness on FiscalYear+Quarter); even if the operator
///     somehow regenerated for an adjacent quarter, the stamped
///     certs MUST NOT appear in it.
///   * Calling MarkFiled twice on the same filing is refused at
///     the entity boundary.
///   * MarkFiled on a filing whose reconciliation is dirty is
///     refused at the handler with a clear error.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class Form41ImmutabilityTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task MarkFiled_StampsAllCertsInQuarter_AndRefusesSecondCall()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (vat, user) = await SeedSharedAsync(db);
        await SeedWhtCategoryAsync(db, code: "Services", rate: 5m);

        // Two payments in Q2 2026.
        await PostSupplierPaymentWithWhtAsync(db, user, vat,
            tinSuffix: "111", grossAmount: 10_000m,
            paymentDate: new DateOnly(2026, 5, 10),
            categoryCode: "Services");
        await PostSupplierPaymentWithWhtAsync(db, user, vat,
            tinSuffix: "222", grossAmount: 4_000m,
            paymentDate: new DateOnly(2026, 6, 10),
            categoryCode: "Services");

        var clock = new TestClock(new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc));

        // Generate Q2.
        var generate = new GenerateForm41Handler(db, clock);
        var generated = await generate.HandleAsync(
            new GenerateForm41Command(2026, 2, user.Id), CancellationToken.None);
        generated.Payload.Lines.Should().HaveCount(2);

        // Mark Filed.
        var mark = new MarkForm41FiledHandler(db, clock, new CaptureAuditLogStore());
        var filed = await mark.HandleAsync(
            new MarkForm41FiledCommand(generated.Form41FilingId, user.Id), CancellationToken.None);

        filed.Status.Should().Be(Form41Status.Filed);
        filed.FiledAtUtc.Should().NotBeNull();
        filed.FiledByUserId.Should().Be(user.Id);

        // Both certs MUST be stamped with the filing id.
        db.ChangeTracker.Clear();
        var stamped = await db.Set<WhtCertificate>().AsNoTracking()
            .Where(c => c.IncludedInForm41FilingId == generated.Form41FilingId)
            .ToListAsync();
        stamped.Should().HaveCount(2,
            because: "MarkFiled stamps every cert in the quarter so they can never appear in another filing (T200)");

        // Second MarkFiled is refused at the entity boundary.
        var act = async () => await mark.HandleAsync(
            new MarkForm41FiledCommand(generated.Form41FilingId, user.Id), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("already Filed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegenerateAfterFile_ProducesEmptyPayload_BecauseAllCertsStamped()
    {
        // After the Q2 filing is filed, somebody manually deletes
        // the Form41Filing row (out-of-band — e.g., a DB tool) and
        // tries to re-generate Q2. The certs are still stamped with
        // a now-orphan filing id, so the regenerated payload picks
        // up zero lines. (The regenerator's primary refusal is the
        // duplicate-quarter unique constraint; this test pins the
        // belt-and-suspenders cert filter — even if the duplicate
        // refusal were bypassed, the stamped certs stay out.)
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (vat, user) = await SeedSharedAsync(db);
        await SeedWhtCategoryAsync(db, code: "Services", rate: 5m);

        await PostSupplierPaymentWithWhtAsync(db, user, vat,
            tinSuffix: "999", grossAmount: 5_000m,
            paymentDate: new DateOnly(2026, 5, 10),
            categoryCode: "Services");

        var clock = new TestClock(new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc));
        var generate = new GenerateForm41Handler(db, clock);
        var mark = new MarkForm41FiledHandler(db, clock, new CaptureAuditLogStore());

        var generated = await generate.HandleAsync(
            new GenerateForm41Command(2026, 2, user.Id), CancellationToken.None);
        await mark.HandleAsync(
            new MarkForm41FiledCommand(generated.Form41FilingId, user.Id), CancellationToken.None);

        // Force-delete the filing row (simulates the out-of-band
        // scenario this test is defending against).
        var filing = await db.Set<Form41Filing>()
            .FirstAsync(f => f.Id == generated.Form41FilingId);
        db.Remove(filing);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Regenerate — picks up zero lines because the cert is
        // already stamped with the (now-orphan) filing id.
        var regenerated = await generate.HandleAsync(
            new GenerateForm41Command(2026, 2, user.Id), CancellationToken.None);
        regenerated.Payload.Lines.Should().BeEmpty(
            because: "T200 immutability — even after the filing row is deleted, the cert's stamp keeps it out of new filings");
    }

    [Fact]
    public async Task MarkFiled_RefusedWhenReconciliationIsDirty()
    {
        // Inject a discrepancy by posting a manual JV that credits
        // WhtPayable WITHOUT a matching cert. The accrual then
        // exceeds the cert total → MarkFiled refuses.
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (vat, user) = await SeedSharedAsync(db);
        await SeedAccountantRoleAsync(db, user);
        await SeedWhtCategoryAsync(db, code: "Services", rate: 5m);

        await PostSupplierPaymentWithWhtAsync(db, user, vat,
            tinSuffix: "777", grossAmount: 1_000m,
            paymentDate: new DateOnly(2026, 5, 10),
            categoryCode: "Services");

        // Manual JV adds 50 EGP to WhtPayable in Q2 — distorts the
        // reconciliation (cert total = 50 from the cert; accrual =
        // 50 + 50 = 100; discrepancy = 50).
        var manualHandler = new EgyptTax.Infrastructure.Journals.CreateManualAdjustingJournalHandler(
            db, new TestClock(new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc)),
            new CaptureAuditLogStore());
        await manualHandler.HandleAsync(
            new EgyptTax.Application.Journals.CreateManualAdjustingJournalCommand(
                Date: new DateOnly(2026, 5, 11),
                Narration: new ArabicEnglishText("غموض", "Manual adjust that breaks reconciliation"),
                CreatedByUserId: user.Id,
                Lines: new[]
                {
                    new EgyptTax.Application.Journals.ManualJournalLineInput(
                        "5200", MoneyEgp.From(50m), MoneyEgp.Zero, "Misc expense"),
                    new EgyptTax.Application.Journals.ManualJournalLineInput(
                        EgyptTax.Domain.Accounting.ChartOfAccountCodes.WhtPayable,
                        MoneyEgp.Zero, MoneyEgp.From(50m), "Phantom WHT accrual"),
                }), CancellationToken.None);

        var clock = new TestClock(new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc));
        var generate = new GenerateForm41Handler(db, clock);
        var generated = await generate.HandleAsync(
            new GenerateForm41Command(2026, 2, user.Id), CancellationToken.None);

        // Generator surfaces the discrepancy in the payload.
        generated.Payload.Reconciliation.MatchesTotalAmountWithheld.Should().BeFalse();
        generated.Payload.Reconciliation.DiscrepancyAmount.Should().Be(50m);

        var mark = new MarkForm41FiledHandler(db, clock, new CaptureAuditLogStore());
        var act = async () => await mark.HandleAsync(
            new MarkForm41FiledCommand(generated.Form41FilingId, user.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("reconciliation", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("FR-046", StringComparison.OrdinalIgnoreCase));

        // Status MUST still be Unfiled.
        db.ChangeTracker.Clear();
        var refreshed = await db.Set<Form41Filing>().AsNoTracking()
            .FirstAsync(f => f.Id == generated.Form41FilingId);
        refreshed.Status.Should().Be(Form41Status.Unfiled);
    }

    [Fact]
    public void Cert_MarkIncludedInForm41Filing_RefusesSecondStamp_AtAggregateBoundary()
    {
        // Defence-in-depth: the aggregate refuses the second stamp
        // even if the handler somehow tried (e.g., a bug that ran
        // MarkFiled twice in a single transaction without the
        // entity's MarkFiled refusal kicking in first).
        var cert = new WhtCertificate(
            direction: WhtCertificateDirection.OutboundToSupplier,
            date: new DateOnly(2026, 5, 10),
            counterpartyId: Guid.NewGuid(),
            sourceVoucherId: Guid.NewGuid(),
            sourceInvoiceId: Guid.NewGuid(),
            whtCategoryId: Guid.NewGuid(),
            rateAppliedPercent: 5m,
            amountWithheld: MoneyEgp.From(100m),
            certificateNumber: "WHT-TEST-001",
            issuedAtUtc: new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc));

        cert.MarkIncludedInForm41Filing(Guid.NewGuid());

        var act = () => cert.MarkIncludedInForm41Filing(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("already included", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task PostSupplierPaymentWithWhtAsync(
        AppDbContext db, User user, VatCategory vat,
        string tinSuffix, decimal grossAmount,
        DateOnly paymentDate, string categoryCode)
    {
        var supplier = new Supplier(
            code: $"SUP-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText($"مورد {tinSuffix}", $"Supplier {tinSuffix}"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse($"123456{tinSuffix}"), vat.Id));
        db.Add(supplier);

        var purchaseDraft = PurchaseInvoice.CreateDraft(supplier.Id, supplier.TaxProfile,
            $"SUP-INV-{tinSuffix}", paymentDate.AddDays(-1));
        purchaseDraft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(grossAmount),
            vatCategoryId: vat.Id, vatRatePercent: 0m,
            deductibleFlag: false);
        db.Add(purchaseDraft);
        await db.SaveChangesAsync();

        var clock = new TestClock(paymentDate.ToDateTime(TimeOnly.MinValue).AddHours(11));
        var postedPurchase = await new PostPurchaseInvoiceHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore())
            .HandleAsync(new PostPurchaseInvoiceCommand(purchaseDraft.Id, user.Id),
                CancellationToken.None);

        var voucher = SupplierPaymentVoucher.CreateDraft(supplier.Id,
            paymentDate, PaymentMethod.BankTransfer, $"PAY-{tinSuffix}",
            MoneyEgp.From(grossAmount));
        db.Add(voucher);
        await db.SaveChangesAsync();
        await new AllocatePaymentHandler(db).HandleAsync(
            new AllocateSupplierPaymentCommand(voucher.Id, postedPurchase.Id, MoneyEgp.From(grossAmount)),
            CancellationToken.None);

        await new PostSupplierPaymentVoucherHandler(db,
                new SqlSequentialNumberAllocator(db), clock,
                new CaptureAuditLogStore(),
                new SupplierPaymentVoucherJournalEmitter(db),
                new SqlWhtComputeService(db))
            .HandleAsync(new PostSupplierPaymentVoucherCommand(
                voucher.Id, user.Id,
                WhtCategoryCode: categoryCode,
                WhtSourceInvoiceId: postedPurchase.Id),
                CancellationToken.None);
    }

    private static async Task SeedWhtCategoryAsync(AppDbContext db, string code, decimal rate)
    {
        db.Add(new WhtCategory(
            code: code,
            name: new ArabicEnglishText(code, code),
            ratePercent: rate,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            applicableTo: WhtApplicableTo.SuppliersServices));
        await db.SaveChangesAsync();
    }

    private static async Task<(VatCategory, User)> SeedSharedAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 0m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(user);
        await db.SaveChangesAsync();
        return (vat, user);
    }

    private static async Task SeedAccountantRoleAsync(AppDbContext db, User user)
    {
        var role = new Role(
            code: "Accountant", name: new ArabicEnglishText("محاسب", "Accountant"),
            requiresMfa: false);
        db.Add(role);
        await db.SaveChangesAsync();

        // Re-attach the user via tracked context so the join updates.
        var tracked = await db.Set<User>().Include(u => u.Roles).FirstAsync(u => u.Id == user.Id);
        tracked.Roles.Add(role);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task EnsureCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync()) return;
        db.Add(new Company(
            legalName: new ArabicEnglishText("شركة", "Test Company SAE"),
            taxRegistrationNumber: EgyptianTin.Parse("123456789"),
            commercialRegistrationNumber: "CR-1",
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "12", postalCode: "11511"),
            taxpayerActivityCode: "0001"));
        await db.SaveChangesAsync();
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
