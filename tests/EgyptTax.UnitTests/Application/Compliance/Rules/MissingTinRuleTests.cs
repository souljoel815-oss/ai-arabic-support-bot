using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

public class MissingTinRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Returns_NoFinding_When_B2BRegistered_Has_Tin()
    {
        var rule = new MissingTinRule();
        var ctx = BuildContext(
            CustomerTaxProfile.B2BRegistered(EgyptianTin.Parse("123456789"), false, VatId)
        );

        rule.Evaluate(ctx)
            .Should()
            .BeEmpty(
                because: "the canonical case — registered customer with a valid TIN — has nothing to flag"
            );
    }

    [Fact]
    public void Returns_BlockerFinding_When_B2BRegistered_Tin_Empty()
    {
        var rule = new MissingTinRule();
        // Hand-construct the snapshot to bypass the factory's TIN
        // requirement — we want to simulate a row that was edited via
        // raw SQL or migrated from a legacy system without a TIN.
        var rotten = new CustomerTaxProfile(
            CustomerTaxProfileType.B2BRegistered,
            TinValue: null,
            VatExemption: false,
            DefaultSalesVatCategoryId: VatId
        );
        var ctx = BuildContext(rotten);

        var findings = rule.Evaluate(ctx);
        findings.Should().HaveCount(1);
        findings[0]
            .Severity.Should()
            .Be(
                RiskSeverity.Blocker,
                because: "B2B-Registered without a TIN is data-integrity rot — the strongest signal possible"
            );
        findings[0].RuleId.Should().Be("SALES_INVOICE.MISSING_TIN");
    }

    [Fact]
    public void Returns_MustFix_When_B2BUnregistered()
    {
        var rule = new MissingTinRule();
        var ctx = BuildContext(CustomerTaxProfile.B2BUnregistered(false, VatId));

        var findings = rule.Evaluate(ctx);
        findings.Should().HaveCount(1);
        findings[0]
            .Severity.Should()
            .Be(
                RiskSeverity.MustFixBeforeFiling,
                because: "B2B traffic without a registered TIN frequently rejects at ETA"
            );
    }

    [Fact]
    public void Returns_NoFinding_For_B2CConsumer()
    {
        var rule = new MissingTinRule();
        var ctx = BuildContext(CustomerTaxProfile.B2CConsumer(false, VatId));

        rule.Evaluate(ctx)
            .Should()
            .BeEmpty(because: "B2C consumer documents legitimately have no TIN");
    }

    private static DocumentRiskContext BuildContext(CustomerTaxProfile snapshot)
    {
        var draft = SalesInvoice.CreateDraft(Guid.NewGuid(), snapshot, new DateOnly(2026, 5, 7));
        draft.AddLine(Guid.NewGuid(), 1m, MoneyEgp.From(100m), VatId, 14m);
        return new DocumentRiskContext(
            Invoice: draft,
            Items: new Dictionary<Guid, Item>(),
            EtaSubmission: null,
            NowUtc: new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)
        );
    }
}
