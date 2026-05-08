using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Documents;

/// <summary>
/// C8 / FR-053 — one allocation row that ties a payment voucher
/// (supplier or customer-receipt) to a specific target document
/// (purchase or sales invoice) for an amount that contributes to
/// closing that target's open balance.
///
/// Parent linkage uses two nullable FKs (one to
/// SupplierPaymentVoucher, one to CustomerReceiptVoucher) with the
/// invariant that EXACTLY ONE is non-null per row. Cleaner than a
/// shared-key + discriminator pattern: each parent voucher's
/// configuration owns its own collection naturally without an
/// extra join table.
///
/// Aggregate-level invariants live on the parent voucher (sum of
/// allocations ≤ gross). Per-target-invoice cap (no allocation
/// exceeds the open balance) is a handler-side check because it
/// requires a DB lookup of currently-open balances on the target.
/// </summary>
public sealed class PaymentAllocation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? SupplierPaymentVoucherId { get; init; }
    public Guid? CustomerReceiptVoucherId { get; init; }
    public Guid TargetDocumentId { get; init; }
    public DocumentType TargetDocumentType { get; init; }
    public MoneyEgp AllocatedAmount { get; init; }

    private PaymentAllocation() { }

    public PaymentAllocation(
        Guid? supplierPaymentVoucherId,
        Guid? customerReceiptVoucherId,
        Guid targetDocumentId,
        DocumentType targetDocumentType,
        MoneyEgp allocatedAmount)
    {
        var parentSet =
            (supplierPaymentVoucherId.HasValue ? 1 : 0)
            + (customerReceiptVoucherId.HasValue ? 1 : 0);
        if (parentSet != 1)
        {
            throw new ArgumentException(
                "Exactly one of {SupplierPaymentVoucherId, CustomerReceiptVoucherId} MUST be non-null.");
        }
        if (targetDocumentId == Guid.Empty)
        {
            throw new ArgumentException("TargetDocumentId is required.", nameof(targetDocumentId));
        }
        if (allocatedAmount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(allocatedAmount),
                "Allocated amount must be positive.");
        }
        // Symmetry: a supplier-payment allocation MUST target a
        // PurchaseInvoice; a customer-receipt allocation MUST target
        // a SalesInvoice or CreditNote. Wrong-side combos are
        // structural bugs the application would never produce
        // intentionally; refuse them at construction.
        if (supplierPaymentVoucherId.HasValue
            && targetDocumentType is not DocumentType.PurchaseInvoice)
        {
            throw new ArgumentException(
                $"Supplier-payment allocations MUST target PurchaseInvoice; got {targetDocumentType}.",
                nameof(targetDocumentType));
        }
        if (customerReceiptVoucherId.HasValue
            && targetDocumentType is not (DocumentType.SalesInvoice or DocumentType.CreditNote))
        {
            throw new ArgumentException(
                $"Customer-receipt allocations MUST target SalesInvoice or CreditNote; got {targetDocumentType}.",
                nameof(targetDocumentType));
        }

        SupplierPaymentVoucherId = supplierPaymentVoucherId;
        CustomerReceiptVoucherId = customerReceiptVoucherId;
        TargetDocumentId = targetDocumentId;
        TargetDocumentType = targetDocumentType;
        AllocatedAmount = allocatedAmount;
    }
}
