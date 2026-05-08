using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

public class MissingEtaCodeRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Returns_NoFinding_When_All_Items_Have_Codes()
    {
        var rule = new MissingEtaCodeRule();
        var ctx = BuildContext(itemsWithCodes: 1, itemsWithoutCodes: 0);

        rule.Evaluate(ctx)
            .Should()
            .BeEmpty(because: "every line's item has an ETA code — nothing to flag");
    }

    [Fact]
    public void Flags_MustFix_When_Any_Item_Missing_Code()
    {
        var rule = new MissingEtaCodeRule();
        var ctx = BuildContext(itemsWithCodes: 1, itemsWithoutCodes: 1);

        var findings = rule.Evaluate(ctx);
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.MustFixBeforeFiling);
        findings[0].RuleId.Should().Be("SALES_INVOICE.MISSING_ETA_ITEM_CODE");
    }

    [Fact]
    public void Counts_Distinct_Items_Once_Across_Multiple_Lines()
    {
        var rule = new MissingEtaCodeRule();
        var (invoice, items) = BuildInvoiceAndItems(itemsWithCodes: 0, itemsWithoutCodes: 1);

        // Same itemId on a second line — the rule reports the missing
        // code once, not twice (the operator fixes it once).
        invoice.AddLine(items.Keys.First(), 2m, MoneyEgp.From(50m), VatId, 14m);

        var ctx = new DocumentRiskContext(
            invoice,
            items,
            null,
            new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)
        );
        rule.Evaluate(ctx).Should().HaveCount(1);
    }

    private static DocumentRiskContext BuildContext(int itemsWithCodes, int itemsWithoutCodes)
    {
        var (invoice, items) = BuildInvoiceAndItems(itemsWithCodes, itemsWithoutCodes);
        return new DocumentRiskContext(
            invoice,
            items,
            null,
            new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)
        );
    }

    private static (SalesInvoice invoice, Dictionary<Guid, Item> items) BuildInvoiceAndItems(
        int itemsWithCodes,
        int itemsWithoutCodes
    )
    {
        var snapshot = CustomerTaxProfile.B2BRegistered(
            EgyptianTin.Parse("123456789"),
            false,
            VatId
        );
        var invoice = SalesInvoice.CreateDraft(Guid.NewGuid(), snapshot, new DateOnly(2026, 5, 7));

        var items = new Dictionary<Guid, Item>();
        for (var i = 0; i < itemsWithCodes; i++)
        {
            var item = new Item(
                $"OK-{i:D2}",
                new ArabicEnglishText("صنف", "Item"),
                VatId,
                etaItemCode: "EGS1.001"
            );
            items[item.Id] = item;
            invoice.AddLine(item.Id, 1m, MoneyEgp.From(100m), VatId, 14m);
        }
        for (var i = 0; i < itemsWithoutCodes; i++)
        {
            var item = new Item($"BAD-{i:D2}", new ArabicEnglishText("صنف", "Item"), VatId);
            items[item.Id] = item;
            invoice.AddLine(item.Id, 1m, MoneyEgp.From(100m), VatId, 14m);
        }
        return (invoice, items);
    }
}
