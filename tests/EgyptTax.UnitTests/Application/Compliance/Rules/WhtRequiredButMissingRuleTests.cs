using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

/// <summary>
/// T212 / FR-045 / Differentiator 1 / US7 — pin the trigger
/// surface for `WhtRequiredButMissingRule`. The rule fires when
/// a deductible purchase from a registered taxpayer is recorded;
/// stays silent for non-deductible / non-registered cases.
/// Severity is intentionally Info (a hint, not a blocker).
/// </summary>
public class WhtRequiredButMissingRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Fires_OnDeductiblePurchase_FromRegisteredTaxpayer_AsInfo()
    {
        var ctx = BuildContext(
            SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("123456789"), VatId),
            deductible: true);

        var findings = new WhtRequiredButMissingRule().Evaluate(ctx);

        findings.Should().HaveCount(1);
        findings[0].RuleId.Should().Be("PURCHASE_INVOICE.WHT_REQUIRED_BUT_MISSING");
        findings[0].Severity.Should().Be(RiskSeverity.Info,
            because: "the rule is a hint, not a blocker — the operator decides whether WHT genuinely applies");
        findings[0].FixHint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Silent_OnNonDeductiblePurchase()
    {
        var ctx = BuildContext(
            SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("123456789"), VatId),
            deductible: false);

        new WhtRequiredButMissingRule().Evaluate(ctx).Should().BeEmpty(
            because: "WHT applies on services + deductible expenses — no deductible line means no hint");
    }

    [Fact]
    public void Silent_OnDeductiblePurchase_FromUnregisteredSupplier()
    {
        var ctx = BuildContext(SupplierTaxProfile.Unregistered(VatId), deductible: true);
        new WhtRequiredButMissingRule().Evaluate(ctx).Should().BeEmpty(
            because: "Unregistered suppliers don't attract WHT the same way as registered taxpayers — silence avoids noise");
    }

    [Fact]
    public void Silent_OnDeductiblePurchase_FromForeignSupplier()
    {
        var ctx = BuildContext(SupplierTaxProfile.ForeignSupplier(VatId), deductible: true);
        new WhtRequiredButMissingRule().Evaluate(ctx).Should().BeEmpty(
            because: "Foreign suppliers go through reverse-charge VAT, not WHT — different compliance regime, different hint");
    }

    private static PurchaseDocumentRiskContext BuildContext(SupplierTaxProfile profile, bool deductible)
    {
        var draft = PurchaseInvoice.CreateDraft(
            Guid.NewGuid(), profile, "SUP-INV-WHT-RULE", new DateOnly(2026, 5, 7));
        draft.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
            quantity: 1m, unitPrice: MoneyEgp.From(1_000m),
            vatCategoryId: VatId, vatRatePercent: 14m, deductibleFlag: deductible);
        return new PurchaseDocumentRiskContext(
            draft, Supplier: null, Attachments: Array.Empty<Attachment>(),
            SupplierInvoiceFingerprints: Array.Empty<PurchaseInvoiceFingerprint>(),
            NowUtc: new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));
    }
}
