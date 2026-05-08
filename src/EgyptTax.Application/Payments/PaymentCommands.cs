using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Payments;

/// <summary>Phase 9 / FR-053 — add an allocation row to a Draft
/// supplier-payment voucher pointing at an outstanding purchase
/// invoice. Handler enforces the per-target-invoice cap (allocation
/// ≤ invoice's open balance) which requires a DB lookup that the
/// aggregate can't do alone.</summary>
public sealed record AllocateSupplierPaymentCommand(
    Guid SupplierPaymentVoucherId,
    Guid TargetPurchaseInvoiceId,
    MoneyEgp AllocatedAmount);

/// <summary>Mirror on the receipts side.</summary>
public sealed record AllocateCustomerReceiptCommand(
    Guid CustomerReceiptVoucherId,
    Guid TargetSalesInvoiceId,
    MoneyEgp AllocatedAmount);

/// <summary>Phase 9 / FR-051 — transition a Draft supplier-payment
/// voucher to Posted. Allocates a `SPV-{year}-{n}` document number,
/// emits the balanced JE (DR AP / CR Cash [+ CR WhtPayable if WHT
/// applied via US7 path]) inside the same SaveChangesAsync so the
/// post + numbering + journal commit atomically.</summary>
public sealed record PostSupplierPaymentVoucherCommand(
    Guid SupplierPaymentVoucherId,
    Guid PostedByUserId);

/// <summary>Mirror on the receipts side.</summary>
public sealed record PostCustomerReceiptVoucherCommand(
    Guid CustomerReceiptVoucherId,
    Guid PostedByUserId);
