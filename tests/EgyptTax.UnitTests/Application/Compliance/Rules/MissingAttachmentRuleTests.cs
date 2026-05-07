using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

public class MissingAttachmentRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Silent_When_No_Deductible_Lines()
    {
        var ctx = BuildContext(deductibleLines: 0, attachments: 0);
        new MissingAttachmentRule().Evaluate(ctx).Should().BeEmpty(
            because: "no deductible lines means FR-016 doesn't apply");
    }

    [Fact]
    public void Silent_When_Deductible_With_Attachment()
    {
        var ctx = BuildContext(deductibleLines: 1, attachments: 1);
        new MissingAttachmentRule().Evaluate(ctx).Should().BeEmpty();
    }

    [Fact]
    public void Blocker_When_Deductible_Without_Attachment()
    {
        var ctx = BuildContext(deductibleLines: 1, attachments: 0);
        var findings = new MissingAttachmentRule().Evaluate(ctx);
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.Blocker,
            because: "FR-016 will reject the post — the badge needs to communicate that pre-emptively");
        findings[0].RuleId.Should().Be("PURCHASE_INVOICE.MISSING_ATTACHMENT");
        findings[0].Title.English.Should().Contain("attachment", Exactly.Once());
    }

    [Fact]
    public void Mentions_Deductible_Line_Count_In_Description()
    {
        var ctx = BuildContext(deductibleLines: 3, attachments: 0);
        var finding = new MissingAttachmentRule().Evaluate(ctx).Single();
        finding.Description.English.Should().Contain("3 deductible line",
            because: "the operator wants to know how many lines are at risk, not just that 'something' is wrong");
    }

    private static PurchaseDocumentRiskContext BuildContext(int deductibleLines, int attachments)
    {
        var supplier = NewRegisteredSupplier();
        var invoice = PurchaseInvoice.CreateDraft(
            supplier.Id, supplier.TaxProfile, "SUP-INV-1", new DateOnly(2026, 5, 7));
        for (var i = 0; i < deductibleLines; i++)
        {
            invoice.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
                quantity: 1m, unitPrice: MoneyEgp.From(100m),
                vatCategoryId: VatId, vatRatePercent: 14m, deductibleFlag: true);
        }
        // Always add at least one non-deductible line if no deductible
        // ones — purchase invoices need a line to exist to be valid.
        if (deductibleLines == 0)
        {
            invoice.AddLine(itemId: null, expenseCategoryId: Guid.NewGuid(),
                quantity: 1m, unitPrice: MoneyEgp.From(100m),
                vatCategoryId: VatId, vatRatePercent: 14m, deductibleFlag: false);
        }

        var attachList = Enumerable.Range(0, attachments)
            .Select(i => new Attachment(
                documentId: invoice.Id,
                documentType: DocumentType.PurchaseInvoice,
                filenameOriginal: $"receipt-{i}.pdf",
                filenameStorage: $"{Guid.NewGuid():N}.pdf",
                relativePath: $"attachments/2026/05/{invoice.Id:D}/{i}.pdf",
                sha256: new byte[32],
                mimeType: "application/pdf",
                sizeBytes: 1024,
                uploadedByUserId: Guid.NewGuid(),
                uploadedAtUtc: new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc)))
            .ToList();

        return new PurchaseDocumentRiskContext(
            invoice, supplier, attachList, new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));
    }

    private static Supplier NewRegisteredSupplier() =>
        new(code: "SUP-001",
            name: new ArabicEnglishText("مورد", "Supplier"),
            address: new ArabicEnglishText("القاهرة", "Cairo"),
            taxProfile: SupplierTaxProfile.RegisteredTaxpayer(
                EgyptianTin.Parse("123456789"), VatId));
}
