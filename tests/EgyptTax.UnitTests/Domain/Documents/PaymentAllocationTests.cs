using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Domain.Documents;

/// <summary>
/// C8 — PaymentAllocation construction invariants. Tests the
/// XOR (one parent), positive-amount, and parent-target-symmetry
/// rules that prevent structural bugs from sneaking through.
/// </summary>
public class PaymentAllocationTests
{
    [Fact]
    public void Construct_RequiresExactlyOneParent_NotBoth()
    {
        var act = () => new PaymentAllocation(
            supplierPaymentVoucherId: Guid.NewGuid(),
            customerReceiptVoucherId: Guid.NewGuid(),
            targetDocumentId: Guid.NewGuid(),
            targetDocumentType: DocumentType.PurchaseInvoice,
            allocatedAmount: MoneyEgp.From(100m));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Exactly one of*");
    }

    [Fact]
    public void Construct_RequiresExactlyOneParent_NotNeither()
    {
        var act = () => new PaymentAllocation(
            supplierPaymentVoucherId: null,
            customerReceiptVoucherId: null,
            targetDocumentId: Guid.NewGuid(),
            targetDocumentType: DocumentType.PurchaseInvoice,
            allocatedAmount: MoneyEgp.From(100m));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SupplierPaymentParent_RequiresPurchaseInvoiceTarget()
    {
        var act = () => new PaymentAllocation(
            supplierPaymentVoucherId: Guid.NewGuid(),
            customerReceiptVoucherId: null,
            targetDocumentId: Guid.NewGuid(),
            targetDocumentType: DocumentType.SalesInvoice,
            allocatedAmount: MoneyEgp.From(100m));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Supplier-payment allocations MUST target PurchaseInvoice*");
    }

    [Fact]
    public void CustomerReceiptParent_RequiresSalesInvoiceOrCreditNoteTarget()
    {
        var act = () => new PaymentAllocation(
            supplierPaymentVoucherId: null,
            customerReceiptVoucherId: Guid.NewGuid(),
            targetDocumentId: Guid.NewGuid(),
            targetDocumentType: DocumentType.PurchaseInvoice,
            allocatedAmount: MoneyEgp.From(100m));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Customer-receipt allocations MUST target SalesInvoice or CreditNote*");
    }

    [Fact]
    public void Construct_RequiresPositiveAmount()
    {
        var act = () => new PaymentAllocation(
            supplierPaymentVoucherId: Guid.NewGuid(),
            customerReceiptVoucherId: null,
            targetDocumentId: Guid.NewGuid(),
            targetDocumentType: DocumentType.PurchaseInvoice,
            allocatedAmount: MoneyEgp.Zero);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(DocumentType.SalesInvoice)]
    [InlineData(DocumentType.CreditNote)]
    public void CustomerReceipt_AcceptsBothSalesInvoiceAndCreditNoteAsTarget(DocumentType targetType)
    {
        // Credit notes are valid customer-receipt targets because a
        // credit note + a payment together net the customer's
        // outstanding balance.
        var allocation = new PaymentAllocation(
            supplierPaymentVoucherId: null,
            customerReceiptVoucherId: Guid.NewGuid(),
            targetDocumentId: Guid.NewGuid(),
            targetDocumentType: targetType,
            allocatedAmount: MoneyEgp.From(100m));
        allocation.AllocatedAmount.Amount.Should().Be(100m);
    }
}
