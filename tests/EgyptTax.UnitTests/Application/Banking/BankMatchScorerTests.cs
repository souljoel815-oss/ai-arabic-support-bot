using EgyptTax.Application.Banking;
using EgyptTax.Domain.Banking;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Banking;

public class BankMatchScorerTests
{
    private static readonly Guid CashAccountId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid VatId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 5, 11);

    [Fact]
    public void ScoreOutflow_ExactMatch_ScoresAtLeastAutoMatch()
    {
        var line = NewLine(debit: 1500m, description: "PAY-001 شركة الأمل");
        var spv = NewSpv(amount: 1500m, paymentRef: "PAY-001");
        var supplier = NewSupplier(nameAr: "شركة الأمل", nameEn: "Hope Co");
        var suppliers = new Dictionary<Guid, Supplier> { [SupplierId] = supplier };

        var result = BankMatchScorer.ScoreOutflow(line, new[] { spv }, suppliers);

        result.Should().NotBeNull();
        result!.SupplierPaymentVoucherId.Should().Be(spv.Id);
        result.Score.Should().BeGreaterOrEqualTo(BankMatchScorer.AutoMatchThreshold);
    }

    [Fact]
    public void ScoreOutflow_AmountOutsideTwoPercent_ReturnsNull()
    {
        var line = NewLine(debit: 1000m, description: "PAY-X");
        // 5% off — outside the ±2% tolerance.
        var spv = NewSpv(amount: 1050m, paymentRef: "PAY-X");
        var suppliers = new Dictionary<Guid, Supplier> { [SupplierId] = NewSupplier() };

        var result = BankMatchScorer.ScoreOutflow(line, new[] { spv }, suppliers);

        result.Should().BeNull();
    }

    [Fact]
    public void ScoreOutflow_DateOutsideSevenDays_ReturnsNull()
    {
        var line = NewLine(debit: 500m, description: "Stale");
        var spv = NewSpv(amount: 500m, paymentDate: Today.AddDays(-30));
        var suppliers = new Dictionary<Guid, Supplier> { [SupplierId] = NewSupplier() };

        var result = BankMatchScorer.ScoreOutflow(line, new[] { spv }, suppliers);

        result.Should().BeNull();
    }

    [Fact]
    public void ScoreOutflow_AmountOnlyMatch_LandsBelowAutoMatch()
    {
        // Amount matches exactly + same day, but no counterparty signal.
        var line = NewLine(debit: 800m, description: "Random text", bankReference: null);
        var spv = NewSpv(amount: 800m, paymentRef: "REF-XYZ");
        var supplier = NewSupplier(nameAr: "مورد آخر", nameEn: "Different Supplier");
        var suppliers = new Dictionary<Guid, Supplier> { [SupplierId] = supplier };

        var result = BankMatchScorer.ScoreOutflow(line, new[] { spv }, suppliers);

        result.Should().NotBeNull();
        // 50 (amount) + 25 (date) + 0 (no counterparty hit) = 75
        result!.Score.Should().Be(75);
        result.Score.Should().BeLessThan(BankMatchScorer.AutoMatchThreshold);
        result.Score.Should().BeGreaterOrEqualTo(BankMatchScorer.SuggestionThreshold);
    }

    [Fact]
    public void ScoreInflow_PicksHigherScoringCandidate()
    {
        // Bank line names a specific counterparty; only the matching
        // customer's voucher should win on counterparty-name overlap.
        var line = NewLine(credit: 2000m, description: "RCT-77 شركة النور");
        var weakCustomerId = Guid.NewGuid();
        var strongCustomerId = Guid.NewGuid();
        var weak = NewCrv(amount: 2000m, paymentRef: "OTHER", customerId: weakCustomerId);
        var strong = NewCrv(amount: 2000m, paymentRef: "OTHER", customerId: strongCustomerId);
        var customers = new Dictionary<Guid, Customer>
        {
            [weakCustomerId] = NewCustomer(nameAr: "عميل بلا صلة", nameEn: "Other Customer"),
            [strongCustomerId] = NewCustomer(nameAr: "شركة النور", nameEn: "Light Co"),
        };

        var result = BankMatchScorer.ScoreInflow(line, new[] { weak, strong }, customers);

        result.Should().NotBeNull();
        result!.CustomerReceiptVoucherId.Should().Be(strong.Id);
    }

    private static BankStatementLine NewLine(
        decimal debit = 0m,
        decimal credit = 0m,
        string description = "test",
        string? bankReference = null)
    {
        var stmt = new BankStatement(
            CashAccountId,
            Today.AddDays(-30),
            Today.AddDays(30),
            MoneyEgp.From(0m),
            MoneyEgp.From(0m),
            "test.pdf",
            DateTime.UtcNow);
        return stmt.AddLine(
            Today,
            description,
            MoneyEgp.From(debit),
            MoneyEgp.From(credit),
            MoneyEgp.From(0m),
            bankReference);
    }

    private static SupplierPaymentVoucher NewSpv(
        decimal amount,
        string paymentRef = "PAY-DEFAULT",
        DateOnly? paymentDate = null)
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            SupplierId,
            paymentDate ?? Today,
            PaymentMethod.BankTransfer,
            paymentRef,
            MoneyEgp.From(amount));
        return v;
    }

    private static CustomerReceiptVoucher NewCrv(
        decimal amount,
        string paymentRef,
        Guid customerId,
        DateOnly? receiptDate = null)
    {
        var v = CustomerReceiptVoucher.CreateDraft(
            customerId,
            receiptDate ?? Today,
            PaymentMethod.BankTransfer,
            paymentRef,
            MoneyEgp.From(amount));
        return v;
    }

    private static Supplier NewSupplier(
        string nameAr = "مورد افتراضي",
        string nameEn = "Default Supplier",
        string? phone = null)
    {
        var s = new Supplier(
            "S-001",
            new ArabicEnglishText(nameAr, nameEn),
            new ArabicEnglishText("", ""),
            SupplierTaxProfile.Unregistered(VatId),
            phone: phone);
        return s;
    }

    private static Customer NewCustomer(
        string nameAr = "عميل افتراضي",
        string nameEn = "Default Customer",
        string? phone = null)
    {
        var addr = PostalAddress.Create(
            new ArabicEnglishText("القاهرة", "Cairo"),
            governorate: "Cairo",
            regionCity: "Nasr City",
            street: "Test St",
            buildingNumber: "1");
        var c = new Customer(
            "C-001",
            new ArabicEnglishText(nameAr, nameEn),
            addr,
            CustomerTaxProfile.B2BUnregistered(false, VatId),
            phone: phone);
        return c;
    }
}
