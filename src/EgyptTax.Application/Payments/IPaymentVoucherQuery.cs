using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Payments;

/// <summary>
/// Phase 9 / T195 — read surface for the payment-voucher Razor
/// pages. Supplies the two outstanding-invoice queries the edit
/// pages need (so the operator can pick what to allocate against)
/// + the per-invoice allocation view.
///
/// "Outstanding" = posted + open_balance > 0, where
/// open_balance = grand_total − SUM(PaymentAllocation against it
/// across ALL vouchers). Same math as the AllocatePaymentHandler
/// uses; the two surfaces stay consistent because both go through
/// the same SQL aggregation.
/// </summary>
public interface IPaymentVoucherQuery
{
    Task<IReadOnlyList<OutstandingInvoiceRow>> ListOutstandingPurchaseInvoicesAsync(
        Guid? supplierFilter = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<OutstandingInvoiceRow>> ListOutstandingSalesInvoicesAsync(
        Guid? customerFilter = null,
        CancellationToken cancellationToken = default
    );

    Task<InvoiceAllocationView?> GetInvoiceAllocationsAsync(
        Guid invoiceId,
        DocumentType invoiceType,
        CancellationToken cancellationToken = default
    );
}

public sealed record OutstandingInvoiceRow(
    Guid InvoiceId,
    string DocumentNumber,
    DateOnly DocumentDate,
    Guid CounterpartyId,
    string CounterpartyName,
    MoneyEgp GrandTotal,
    MoneyEgp AllocatedToDate,
    MoneyEgp OpenBalance
);

public sealed record InvoiceAllocationView(
    Guid InvoiceId,
    DocumentType InvoiceType,
    string DocumentNumber,
    DateOnly DocumentDate,
    MoneyEgp GrandTotal,
    MoneyEgp AllocatedToDate,
    MoneyEgp OpenBalance,
    IReadOnlyList<AllocationRow> Allocations
);

public sealed record AllocationRow(
    Guid AllocationId,
    Guid? SupplierPaymentVoucherId,
    Guid? CustomerReceiptVoucherId,
    string? VoucherDocumentNumber,
    DateOnly VoucherDate,
    MoneyEgp AllocatedAmount
);
