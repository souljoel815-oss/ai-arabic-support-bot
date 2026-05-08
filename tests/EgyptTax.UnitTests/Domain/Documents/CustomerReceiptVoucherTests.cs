using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Domain.Documents;

/// <summary>
/// C7 / FR-052 / FR-053 / Phase 9 — customer receipt voucher
/// aggregate invariants. Mirror of the supplier-payment tests
/// on the receipts side.
/// </summary>
public class CustomerReceiptVoucherTests
{
    [Fact]
    public void CreateDraft_StartsInDraft_WithGrossEqualToNet_AndZeroWht()
    {
        var v = CustomerReceiptVoucher.CreateDraft(
            customerId: Guid.NewGuid(),
            receiptDate: new DateOnly(2026, 5, 9),
            paymentMethod: PaymentMethod.BankTransfer,
            paymentReference: "RCV-001",
            grossReceiptAmount: MoneyEgp.From(8_500m));

        v.State.Should().Be(DocumentState.Draft);
        v.GrossReceiptAmount.Amount.Should().Be(8_500m);
        v.WhtReceivableAmount.Amount.Should().Be(0m);
        v.NetCashReceived.Amount.Should().Be(8_500m);
        v.CustomerWhtCertificateId.Should().BeNull();
    }

    [Fact]
    public void AddAllocation_RespectsVoucherCap_PerFr053()
    {
        var v = CustomerReceiptVoucher.CreateDraft(Guid.NewGuid(),
            new DateOnly(2026, 5, 9), PaymentMethod.Cash, "REF",
            MoneyEgp.From(1_000m));

        v.AddAllocation(Guid.NewGuid(), MoneyEgp.From(800m));

        var act = () => v.AddAllocation(Guid.NewGuid(), MoneyEgp.From(300m));
        act.Should().Throw<InvalidOperationException>(
            because: "FR-053 — sum of allocations cannot exceed gross receipt");
        v.Allocations.Should().HaveCount(1);
    }

    [Fact]
    public void MarkPosted_RequiresAtLeastOneAllocation()
    {
        var v = CustomerReceiptVoucher.CreateDraft(Guid.NewGuid(),
            new DateOnly(2026, 5, 9), PaymentMethod.Cash, "REF",
            MoneyEgp.From(500m));

        var act = () => v.MarkPosted("RCV-001", Guid.NewGuid(),
            new DateTime(2026, 5, 9, 10, 0, 0, DateTimeKind.Utc));
        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("at least one allocation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyCustomerWhtCertificate_SplitsGrossIntoCashPlusWhtReceivable()
    {
        var v = CustomerReceiptVoucher.CreateDraft(Guid.NewGuid(),
            new DateOnly(2026, 5, 9), PaymentMethod.BankTransfer, "REF",
            MoneyEgp.From(10_000m));

        var certId = Guid.NewGuid();
        v.ApplyCustomerWhtCertificate(MoneyEgp.From(750m), certId);

        v.WhtReceivableAmount.Amount.Should().Be(750m);
        v.NetCashReceived.Amount.Should().Be(9_250m);
        v.CustomerWhtCertificateId.Should().Be(certId);
    }
}
