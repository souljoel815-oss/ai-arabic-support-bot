using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

public class NonRecoverableInputVatRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Silent_When_Supplier_Is_Registered()
    {
        var ctx = BuildContext(
            SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("123456789"), VatId),
            deductible: true
        );
        new NonRecoverableInputVatRule()
            .Evaluate(ctx)
            .Should()
            .BeEmpty(because: "registered taxpayer → input VAT IS recoverable per FR-020");
    }

    [Fact]
    public void Silent_When_Unregistered_But_No_Deductible_Lines()
    {
        var ctx = BuildContext(SupplierTaxProfile.Unregistered(VatId), deductible: false);
        new NonRecoverableInputVatRule()
            .Evaluate(ctx)
            .Should()
            .BeEmpty(because: "operator hasn't claimed deductibility — there's nothing to flag");
    }

    [Fact]
    public void MustFix_When_Unregistered_And_Deductible()
    {
        var ctx = BuildContext(SupplierTaxProfile.Unregistered(VatId), deductible: true);
        var findings = new NonRecoverableInputVatRule().Evaluate(ctx);
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.MustFixBeforeFiling);
        findings[0].RuleId.Should().Be("PURCHASE_INVOICE.NON_RECOVERABLE_INPUT_VAT");
        findings[0].Title.English.Should().Contain("Unregistered");
    }

    [Fact]
    public void MustFix_When_ForeignSupplier_And_Deductible_With_ReverseCharge_FixHint()
    {
        var ctx = BuildContext(SupplierTaxProfile.ForeignSupplier(VatId), deductible: true);
        var finding = new NonRecoverableInputVatRule().Evaluate(ctx).Single();
        finding.Title.English.Should().Contain("Foreign supplier");
        finding
            .FixHint.Should()
            .Contain(
                "reverse-charge",
                because: "the FixHint should steer the operator to the right surface for foreign suppliers"
            );
    }

    [Fact]
    public void Reads_Snapshot_Not_Live_Supplier_Profile()
    {
        // The invoice was posted with the supplier's profile snapshotted
        // as Unregistered. Later the live supplier became Registered.
        // The rule still flags the invoice — historical decisions are
        // judged on what was true at post-time.
        var draft = PurchaseInvoice.CreateDraft(
            Guid.NewGuid(),
            SupplierTaxProfile.Unregistered(VatId), // snapshot
            "SUP-INV-99",
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(
            null,
            Guid.NewGuid(),
            1m,
            MoneyEgp.From(500m),
            VatId,
            14m,
            deductibleFlag: true
        );

        var laterRegisteredSupplier = new Supplier(
            code: "SUP-99",
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("999999999"), VatId)
        );

        var ctx = new PurchaseDocumentRiskContext(
            draft,
            laterRegisteredSupplier,
            Array.Empty<Attachment>(),
            SupplierInvoiceFingerprints: Array.Empty<PurchaseInvoiceFingerprint>(),
            NowUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        new NonRecoverableInputVatRule()
            .Evaluate(ctx)
            .Should()
            .HaveCount(
                1,
                because: "the rule reads invoice.SupplierTaxProfileSnapshot, not the live supplier — historical decisions stay stable"
            );
    }

    private static PurchaseDocumentRiskContext BuildContext(
        SupplierTaxProfile profile,
        bool deductible
    )
    {
        var draft = PurchaseInvoice.CreateDraft(
            Guid.NewGuid(),
            profile,
            "SUP-INV-1",
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(
            itemId: null,
            expenseCategoryId: Guid.NewGuid(),
            quantity: 1m,
            unitPrice: MoneyEgp.From(1_000m),
            vatCategoryId: VatId,
            vatRatePercent: 14m,
            deductibleFlag: deductible
        );
        return new PurchaseDocumentRiskContext(
            draft,
            Supplier: null,
            Attachments: Array.Empty<Attachment>(),
            SupplierInvoiceFingerprints: Array.Empty<PurchaseInvoiceFingerprint>(),
            NowUtc: new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)
        );
    }
}
