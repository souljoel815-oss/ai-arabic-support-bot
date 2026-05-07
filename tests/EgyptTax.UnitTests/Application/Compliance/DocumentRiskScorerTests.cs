using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance;

public class DocumentRiskScorerTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Aggregates_Findings_From_Every_Rule()
    {
        var scorer = new DocumentRiskScorer(new IDocumentRiskRule[]
        {
            new MissingTinRule(),
            new MissingEtaCodeRule(),
        });
        // B2B-Unregistered (TIN missing) + an item without ETA code →
        // both rules should fire.
        var snapshot = CustomerTaxProfile.B2BUnregistered(false, VatId);
        var invoice = SalesInvoice.CreateDraft(Guid.NewGuid(), snapshot, new DateOnly(2026, 5, 7));
        var item = new Item("BAD-1", new ArabicEnglishText("صنف", "Item"), VatId);
        invoice.AddLine(item.Id, 1m, MoneyEgp.From(100m), VatId, 14m);
        var ctx = new DocumentRiskContext(invoice,
            new Dictionary<Guid, Item> { [item.Id] = item },
            null, new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));

        var findings = scorer.Score(ctx);
        findings.Should().HaveCount(2);
        findings.Should().Contain(f => f.RuleId == "SALES_INVOICE.MISSING_TIN");
        findings.Should().Contain(f => f.RuleId == "SALES_INVOICE.MISSING_ETA_ITEM_CODE");
    }

    [Fact]
    public void Sorts_Findings_By_Severity_Desc_Then_RuleId_Asc()
    {
        var scorer = new DocumentRiskScorer(new IDocumentRiskRule[]
        {
            new InfoStubRule("STUB.INFO_B"),
            new InfoStubRule("STUB.INFO_A"),
            new BlockerStubRule(),
        });
        var ctx = new DocumentRiskContext(
            SalesInvoice.CreateDraft(Guid.NewGuid(),
                CustomerTaxProfile.B2CConsumer(false, VatId),
                new DateOnly(2026, 5, 7)),
            new Dictionary<Guid, Item>(), null,
            new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));

        var findings = scorer.Score(ctx);
        findings.Should().HaveCount(3);
        findings[0].Severity.Should().Be(RiskSeverity.Blocker,
            because: "Blocker outranks Info; the badge picks the headline finding off index 0");
        findings[1].RuleId.Should().Be("STUB.INFO_A",
            because: "ties on severity break by RuleId ascending so the order is stable run-to-run");
        findings[2].RuleId.Should().Be("STUB.INFO_B");
    }

    [Fact]
    public void HighestSeverity_Returns_Null_For_Empty_Findings()
    {
        DocumentRiskScorer.HighestSeverity(Array.Empty<RiskFinding>()).Should().BeNull();
    }

    [Fact]
    public void HighestSeverity_Returns_Worst_Finding()
    {
        var findings = new[]
        {
            new RiskFinding("A", RiskSeverity.Warning, default, default),
            new RiskFinding("B", RiskSeverity.Blocker, default, default),
            new RiskFinding("C", RiskSeverity.Info, default, default),
        };
        DocumentRiskScorer.HighestSeverity(findings).Should().Be(RiskSeverity.Blocker);
    }

    private sealed class InfoStubRule(string id) : IDocumentRiskRule
    {
        public string RuleId { get; } = id;
        public IReadOnlyList<RiskFinding> Evaluate(DocumentRiskContext context) =>
            [new RiskFinding(RuleId, RiskSeverity.Info, default, default)];
    }

    private sealed class BlockerStubRule : IDocumentRiskRule
    {
        public string RuleId => "STUB.BLOCKER";
        public IReadOnlyList<RiskFinding> Evaluate(DocumentRiskContext context) =>
            [new RiskFinding(RuleId, RiskSeverity.Blocker, default, default)];
    }
}
