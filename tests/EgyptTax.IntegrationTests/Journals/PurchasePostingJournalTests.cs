using EgyptTax.Application.Audit;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Purchases;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Journals;

/// <summary>
/// T165 / US4 scenario 2 — buy-side auto-emit contract. Posting a
/// deductible purchase invoice with subtotal 500 EGP + VAT 70 EGP
/// MUST produce a balanced journal entry:
///
///   * DR GenericExpense        500
///   * DR InputVatRecoverable    70
///   * CR AccountsPayable       570
///
/// Plus a non-deductible-line variant: the input VAT is sunk into
/// the expense account (no recoverable VAT debit).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PurchasePostingJournalTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task PostedDeductiblePurchase_500Plus70Vat_Emits_Expense_InputVat_Ap_BalancedJournal()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id, supplier.TaxProfile, "SUP-INV-T165", new DateOnly(2026, 5, 9));
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(500m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: true);
        db.Add(draft);

        // FR-016 — deductible purchase requires an attachment.
        db.Add(new Attachment(
            documentId: draft.Id, documentType: DocumentType.PurchaseInvoice,
            filenameOriginal: "supplier-receipt.pdf",
            filenameStorage: $"{Guid.NewGuid():N}.pdf",
            relativePath: $"attachments/2026/05/{draft.Id:D}/supplier-receipt.pdf",
            sha256: new byte[32], mimeType: "application/pdf",
            sizeBytes: 1234, uploadedByUserId: user.Id,
            uploadedAtUtc: new DateTime(2026, 5, 9, 9, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var emitter = new PurchaseInvoiceJournalEmitter(db);
        var handler = new PostPurchaseInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), clock,
            new CaptureAuditLogStore(), emitter);
        var posted = await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, user.Id), CancellationToken.None);

        db.ChangeTracker.Clear();
        var je = await db.Set<JournalEntry>().AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);

        je.Lines.Should().HaveCount(3);
        je.Lines.Sum(l => l.Debit.Amount).Should().Be(570m);
        je.Lines.Sum(l => l.Credit.Amount).Should().Be(570m,
            because: "US4 scenario 2 — buy-side balance MUST equal 570 (500 expense + 70 input VAT)");

        var expense = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.GenericExpense);
        var inputVat = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.InputVatRecoverable);
        var ap = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsPayable);

        expense.Debit.Amount.Should().Be(500m);
        expense.Credit.Amount.Should().Be(0m);
        inputVat.Debit.Amount.Should().Be(70m);
        inputVat.Credit.Amount.Should().Be(0m);
        ap.Debit.Amount.Should().Be(0m);
        ap.Credit.Amount.Should().Be(570m);
    }

    [Fact]
    public async Task PostedNonDeductiblePurchase_RollsVatIntoExpense_AndOmitsInputVatRow()
    {
        // Non-deductible VAT is non-recoverable — accounting rolls
        // it into the expense account rather than sitting on the
        // balance sheet as recoverable input VAT. The InputVAT
        // row MUST be omitted (a zero-amount line would violate
        // the JournalEntryLine "debit XOR credit > 0" invariant).
        await using var db = await _fixture.CreateContextAsync();
        var (supplier, vat, user) = await SeedAsync(db);

        var draft = PurchaseInvoice.CreateDraft(
            supplier.Id, supplier.TaxProfile, "SUP-INV-NONDED", new DateOnly(2026, 5, 9));
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(500m),
            vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent,
            deductibleFlag: false);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 9, 11, 0, 0, DateTimeKind.Utc));
        var handler = new PostPurchaseInvoiceHandler(db,
            new SqlSequentialNumberAllocator(db), clock,
            new CaptureAuditLogStore(), new PurchaseInvoiceJournalEmitter(db));
        var posted = await handler.HandleAsync(
            new PostPurchaseInvoiceCommand(draft.Id, user.Id), CancellationToken.None);

        db.ChangeTracker.Clear();
        var je = await db.Set<JournalEntry>().AsNoTracking()
            .Include(e => e.Lines)
            .FirstAsync(e => e.SourceDocumentId == posted.Id);

        je.Lines.Should().HaveCount(2,
            because: "non-deductible purchase emits exactly Expense + AP — no Input VAT row");
        je.Lines.Should().NotContain(l => l.AccountCode == ChartOfAccountCodes.InputVatRecoverable);

        var expense = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.GenericExpense);
        expense.Debit.Amount.Should().Be(570m,
            because: "non-deductible VAT is rolled INTO the expense (500 + 70)");
        var ap = je.Lines.Single(l => l.AccountCode == ChartOfAccountCodes.AccountsPayable);
        ap.Credit.Amount.Should().Be(570m);
    }

    private static async Task<(Supplier, VatCategory, User)> SeedAsync(AppDbContext db)
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
