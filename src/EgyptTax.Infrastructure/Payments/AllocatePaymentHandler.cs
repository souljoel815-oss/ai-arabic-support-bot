using EgyptTax.Application.Payments;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Payments;

/// <summary>
/// Phase 9 / FR-053 — allocates an amount on a Draft payment
/// voucher to a specific posted invoice. Two checks fire here that
/// the aggregate cannot do alone:
///
///   (a) The target invoice MUST be Posted (allocating against a
///       Draft is non-sensical — the invoice has no open balance
///       yet).
///   (b) The allocated amount MUST NOT exceed the invoice's
///       remaining open balance, where:
///         open_balance(invoice) =
///             grand_total
///             - SUM(existing PaymentAllocation amounts targeting it
///                   across ALL vouchers, including the current draft)
///       Per FR-053, this prevents over-allocation across multiple
///       vouchers — a customer who paid 1000 against a 600 invoice
///       can't accidentally over-credit themselves.
///
/// The voucher cap (sum of allocations ≤ gross) is still enforced
/// inside the aggregate's AddAllocation; this handler layers the
/// per-target invoice cap on top.
/// </summary>
public sealed class AllocatePaymentHandler
{
    private readonly AppDbContext _db;

    public AllocatePaymentHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentAllocation> HandleAsync(
        AllocateSupplierPaymentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var voucher =
            await _db.Set<SupplierPaymentVoucher>()
                .Include(v => v.Allocations)
                .FirstOrDefaultAsync(
                    v => v.Id == command.SupplierPaymentVoucherId,
                    cancellationToken
                )
            ?? throw new InvalidOperationException(
                $"SupplierPaymentVoucher {command.SupplierPaymentVoucherId} not found."
            );

        var invoice =
            await _db.Set<PurchaseInvoice>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Id == command.TargetPurchaseInvoiceId,
                    cancellationToken
                )
            ?? throw new InvalidOperationException(
                $"Target PurchaseInvoice {command.TargetPurchaseInvoiceId} not found."
            );

        if (invoice.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot allocate to PurchaseInvoice {invoice.Id}: state is {invoice.State}, not Posted. Allocations only apply to posted invoices with an open balance."
            );
        }

        await EnsureWithinOpenBalanceAsync(
            command.TargetPurchaseInvoiceId,
            invoice.GrandTotal.Amount,
            command.AllocatedAmount.Amount,
            cancellationToken
        );

        var allocation = voucher.AddAllocation(
            command.TargetPurchaseInvoiceId,
            command.AllocatedAmount
        );
        await _db.SaveChangesAsync(cancellationToken);
        return allocation;
    }

    public async Task<PaymentAllocation> HandleAsync(
        AllocateCustomerReceiptCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var voucher =
            await _db.Set<CustomerReceiptVoucher>()
                .Include(v => v.Allocations)
                .FirstOrDefaultAsync(
                    v => v.Id == command.CustomerReceiptVoucherId,
                    cancellationToken
                )
            ?? throw new InvalidOperationException(
                $"CustomerReceiptVoucher {command.CustomerReceiptVoucherId} not found."
            );

        var invoice =
            await _db.Set<SalesInvoice>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == command.TargetSalesInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Target SalesInvoice {command.TargetSalesInvoiceId} not found."
            );

        if (invoice.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                $"Cannot allocate to SalesInvoice {invoice.Id}: state is {invoice.State}, not Posted. Allocations only apply to posted invoices with an open balance."
            );
        }

        await EnsureWithinOpenBalanceAsync(
            command.TargetSalesInvoiceId,
            // Credit-note totals are negative; for the open-balance
            // computation we work with the absolute magnitude on
            // the AR side. Sales invoices are positive so this is
            // a no-op for the common path.
            Math.Abs(invoice.GrandTotal.Amount),
            command.AllocatedAmount.Amount,
            cancellationToken
        );

        var targetType = invoice.IsCreditNote ? DocumentType.CreditNote : DocumentType.SalesInvoice;
        var allocation = voucher.AddAllocation(
            command.TargetSalesInvoiceId,
            command.AllocatedAmount,
            targetType
        );
        await _db.SaveChangesAsync(cancellationToken);
        return allocation;
    }

    private async Task EnsureWithinOpenBalanceAsync(
        Guid targetDocumentId,
        decimal grandTotal,
        decimal newAllocationAmount,
        CancellationToken cancellationToken
    )
    {
        var alreadyAllocated = await _db.Set<PaymentAllocation>()
            .AsNoTracking()
            .Where(a => a.TargetDocumentId == targetDocumentId)
            .Select(a => a.AllocatedAmount.Amount)
            .ToListAsync(cancellationToken);
        var alreadySum = alreadyAllocated.Sum();
        var openBalance = grandTotal - alreadySum;

        if (newAllocationAmount > openBalance)
        {
            throw new InvalidOperationException(
                $"Cannot allocate {newAllocationAmount:F2} to invoice {targetDocumentId}: "
                    + $"open balance is {openBalance:F2} (grand total {grandTotal:F2} − already allocated {alreadySum:F2}). "
                    + "FR-053 — no allocation may exceed the target's open balance."
            );
        }
    }
}
