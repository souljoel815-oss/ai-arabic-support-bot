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
    MoneyEgp AllocatedAmount
);

/// <summary>Mirror on the receipts side.</summary>
public sealed record AllocateCustomerReceiptCommand(
    Guid CustomerReceiptVoucherId,
    Guid TargetSalesInvoiceId,
    MoneyEgp AllocatedAmount
);

/// <summary>Phase 9 / FR-051 — transition a Draft supplier-payment
/// voucher to Posted. Allocates a `SPV-{year}-{n}` document number,
/// emits the balanced JE (DR AP / CR Cash [+ CR WhtPayable if WHT
/// applied via US7 path]) inside the same SaveChangesAsync so the
/// post + numbering + journal commit atomically.
///
/// FR-045 / US7 — when <see cref="WhtCategoryCode"/> +
/// <see cref="WhtSourceInvoiceId"/> are supplied, the post handler
/// calls <c>WhtComputeService</c> with the voucher's payment date,
/// generates an outbound <c>WhtCertificate</c>, and applies the
/// WHT split on the voucher BEFORE MarkPosted — so the emitted
/// JE is the 3-line form (DR AP / CR Cash net / CR WhtPayable)
/// and the certificate's id is back-pointed on the voucher.</summary>
public sealed record PostSupplierPaymentVoucherCommand(
    Guid SupplierPaymentVoucherId,
    Guid PostedByUserId,
    string? WhtCategoryCode = null,
    Guid? WhtSourceInvoiceId = null
);

/// <summary>Mirror on the receipts side.
///
/// FR-052 / US7 — when the customer issued a WHT certificate,
/// supply the recorded certificate number + amount + category +
/// source invoice. The post handler creates an inbound
/// <c>WhtCertificate</c> row, applies the customer cert on the
/// voucher, and the emitter produces the 3-line JE
/// (DR Cash / DR WhtReceivable / CR AR).</summary>
public sealed record PostCustomerReceiptVoucherCommand(
    Guid CustomerReceiptVoucherId,
    Guid PostedByUserId,
    string? CustomerWhtCertificateNumber = null,
    decimal? CustomerWhtAmount = null,
    string? WhtCategoryCode = null,
    Guid? WhtSourceInvoiceId = null
);
