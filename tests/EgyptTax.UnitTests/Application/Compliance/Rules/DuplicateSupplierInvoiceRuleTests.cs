using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

public class DuplicateSupplierInvoiceRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    [Fact]
    public void Silent_When_No_Fingerprints_Loaded()
    {
        var ctx = BuildContext("SUP-INV-001", new DateOnly(2026, 5, 7), 1140m,
            fingerprints: Array.Empty<PurchaseInvoiceFingerprint>());
        new DuplicateSupplierInvoiceRule().Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void Silent_When_Fingerprint_Is_Self()
    {
        // The subject document is in the fingerprint list (e.g. when
        // re-scoring a posted document where the SQL probe returned
        // the row itself). The rule must ignore self via the Id
        // filter. Build the draft first; then construct the
        // fingerprint with the draft's Id.
        var ctx = BuildContext("SUP-INV-001", new DateOnly(2026, 5, 7), 1140m,
            fingerprints: Array.Empty<PurchaseInvoiceFingerprint>());
        var selfFingerprint = new PurchaseInvoiceFingerprint(
            ctx.Invoice.Id, "PI-2026-000001", "SUP-INV-001",
            new DateOnly(2026, 5, 7), MoneyEgp.From(1140m));
        var ctxWithSelf = ctx with { SupplierInvoiceFingerprints = new[] { selfFingerprint } };

        new DuplicateSupplierInvoiceRule().Evaluate(ctxWithSelf).Should().BeEmpty(
            because: "the rule's `Where(f => f.Id != subject.Id)` filter must not flag the subject against itself");
    }

    [Fact]
    public void Blocker_When_Supplier_Number_Date_Amount_All_Match()
    {
        var fingerprints = new[]
        {
            new PurchaseInvoiceFingerprint(
                Guid.NewGuid(), "PI-2026-000099", "SUP-INV-001",
                new DateOnly(2026, 5, 7), MoneyEgp.From(1140m)),
        };
        var ctx = BuildContext("SUP-INV-001", new DateOnly(2026, 5, 7), 1140m, fingerprints);

        var findings = new DuplicateSupplierInvoiceRule().Evaluate(ctx);
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.Blocker,
            because: "all four fingerprint columns match — almost certainly the same physical document keyed in twice");
        findings[0].RuleId.Should().Be("PURCHASE_INVOICE.DUPLICATE_SUPPLIER_INVOICE");
        findings[0].Description.English.Should().Contain("PI-2026-000099",
            because: "the operator needs the prior document number to drill into it");
    }

    [Fact]
    public void MustFix_When_Number_Matches_But_Amount_Differs()
    {
        var fingerprints = new[]
        {
            new PurchaseInvoiceFingerprint(
                Guid.NewGuid(), "PI-2026-000099", "SUP-INV-001",
                new DateOnly(2026, 5, 7), MoneyEgp.From(2000m)), // different amount
        };
        var ctx = BuildContext("SUP-INV-001", new DateOnly(2026, 5, 7), 1140m, fingerprints);

        var finding = new DuplicateSupplierInvoiceRule().Evaluate(ctx).Single();
        finding.Severity.Should().Be(RiskSeverity.MustFixBeforeFiling,
            because: "supplier reference number reused but amount differs — could be a re-issue, but operator must verify");
    }

    [Fact]
    public void Number_Comparison_Is_Case_Insensitive_And_Trimmed()
    {
        var fingerprints = new[]
        {
            new PurchaseInvoiceFingerprint(
                Guid.NewGuid(), "PI-2026-000099", "  sup-inv-001  ",
                new DateOnly(2026, 5, 7), MoneyEgp.From(1140m)),
        };
        var ctx = BuildContext("SUP-INV-001", new DateOnly(2026, 5, 7), 1140m, fingerprints);

        new DuplicateSupplierInvoiceRule().Evaluate(ctx).Should().HaveCount(1,
            because: "operators routinely typo whitespace + casing on supplier numbers; the dedup signal must survive trivial differences");
    }

    [Fact]
    public void Single_Finding_Even_When_Multiple_Candidates_Match()
    {
        var fingerprints = new[]
        {
            new PurchaseInvoiceFingerprint(Guid.NewGuid(), "PI-2026-000050", "SUP-INV-001",
                new DateOnly(2026, 5, 7), MoneyEgp.From(1140m)),
            new PurchaseInvoiceFingerprint(Guid.NewGuid(), "PI-2026-000099", "SUP-INV-001",
                new DateOnly(2026, 5, 7), MoneyEgp.From(1140m)),
        };
        var ctx = BuildContext("SUP-INV-001", new DateOnly(2026, 5, 7), 1140m, fingerprints);

        new DuplicateSupplierInvoiceRule().Evaluate(ctx).Should().HaveCount(1,
            because: "one finding is enough to surface the issue — the rule shouldn't spam the badge with N near-identical entries");
    }

    private static PurchaseDocumentRiskContext BuildContext(
        string supplierInvoiceNumber,
        DateOnly dateReceived,
        decimal grandTotal,
        IReadOnlyList<PurchaseInvoiceFingerprint> fingerprints)
    {
        var profile = SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("123456789"), VatId);
        var draft = PurchaseInvoice.CreateDraft(SupplierId, profile, supplierInvoiceNumber, dateReceived);

        // Compute the unit price that yields the requested grand
        // total under 14% VAT so the rule's exact-match path can be
        // exercised (subtotal * 1.14 = grandTotal). Banker's rounding
        // means 1140 EGP / 1.14 = 1000.00 exact; for 2000 EGP we get
        // 1754.39, grand-total 2000.00 — close enough for the test.
        var subtotal = grandTotal / 1.14m;
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(subtotal),
            vatCategoryId: VatId, vatRatePercent: 14m, deductibleFlag: false);

        return new PurchaseDocumentRiskContext(
            draft, Supplier: null, Attachments: Array.Empty<Attachment>(),
            SupplierInvoiceFingerprints: fingerprints,
            NowUtc: new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));
    }
}
