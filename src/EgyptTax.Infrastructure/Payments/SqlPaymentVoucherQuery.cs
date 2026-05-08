using EgyptTax.Application.Payments;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Payments;

/// <summary>
/// Phase 9 / T195 — EF-backed read surface for the payment-voucher
/// pages. Each method composes the open_balance math the same way
/// the AllocatePaymentHandler does so the surfaces stay consistent.
/// </summary>
public sealed class SqlPaymentVoucherQuery : IPaymentVoucherQuery
{
    private readonly AppDbContext _db;

    public SqlPaymentVoucherQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OutstandingInvoiceRow>> ListOutstandingPurchaseInvoicesAsync(
        Guid? supplierFilter = null, CancellationToken cancellationToken = default)
    {
        var invoices = await _db.Set<PurchaseInvoice>().AsNoTracking()
            .Where(p => p.State == DocumentState.Posted
                && (supplierFilter == null || p.SupplierId == supplierFilter))
            .OrderByDescending(p => p.DateReceived).ThenBy(p => p.DocumentNumber)
            .ToListAsync(cancellationToken);

        var supplierIds = invoices.Select(i => i.SupplierId).Distinct().ToArray();
        var supplierNames = await _db.Set<Supplier>().AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name.English, cancellationToken);

        return await BuildOutstandingRowsAsync(
            invoices.Select(i => (i.Id, i.DocumentNumber ?? "", i.DateReceived, i.SupplierId,
                supplierNames.GetValueOrDefault(i.SupplierId, "(unknown)"),
                i.GrandTotal.Amount)).ToList(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<OutstandingInvoiceRow>> ListOutstandingSalesInvoicesAsync(
        Guid? customerFilter = null, CancellationToken cancellationToken = default)
    {
        var invoices = await _db.Set<SalesInvoice>().AsNoTracking()
            .Where(s => s.State == DocumentState.Posted
                && s.CreditNoteOfInvoiceId == null
                && (customerFilter == null || s.CustomerId == customerFilter))
            .OrderByDescending(s => s.DocumentDate).ThenBy(s => s.DocumentNumber)
            .ToListAsync(cancellationToken);

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToArray();
        var customerNames = await _db.Set<Customer>().AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name.English, cancellationToken);

        return await BuildOutstandingRowsAsync(
            invoices.Select(i => (i.Id, i.DocumentNumber ?? "", i.DocumentDate, i.CustomerId,
                customerNames.GetValueOrDefault(i.CustomerId, "(unknown)"),
                i.GrandTotal.Amount)).ToList(),
            cancellationToken);
    }

    public async Task<InvoiceAllocationView?> GetInvoiceAllocationsAsync(
        Guid invoiceId, DocumentType invoiceType, CancellationToken cancellationToken = default)
    {
        // Resolve the invoice to surface its number / date / total.
        decimal grandTotal;
        string documentNumber;
        DateOnly documentDate;

        if (invoiceType == DocumentType.PurchaseInvoice)
        {
            var p = await _db.Set<PurchaseInvoice>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
            if (p is null) return null;
            grandTotal = p.GrandTotal.Amount;
            documentNumber = p.DocumentNumber ?? "(draft)";
            documentDate = p.DateReceived;
        }
        else
        {
            var s = await _db.Set<SalesInvoice>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
            if (s is null) return null;
            grandTotal = Math.Abs(s.GrandTotal.Amount); // credit-notes are negative; show magnitude
            documentNumber = s.DocumentNumber ?? "(draft)";
            documentDate = s.DocumentDate;
        }

        var allocations = await _db.Set<PaymentAllocation>().AsNoTracking()
            .Where(a => a.TargetDocumentId == invoiceId)
            .ToListAsync(cancellationToken);

        // Resolve voucher document numbers + dates for the allocation rows.
        var spvIds = allocations.Where(a => a.SupplierPaymentVoucherId.HasValue)
            .Select(a => a.SupplierPaymentVoucherId!.Value).Distinct().ToArray();
        var crvIds = allocations.Where(a => a.CustomerReceiptVoucherId.HasValue)
            .Select(a => a.CustomerReceiptVoucherId!.Value).Distinct().ToArray();

        var spvLookup = await _db.Set<SupplierPaymentVoucher>().AsNoTracking()
            .Where(v => spvIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => (v.DocumentNumber, v.PaymentDate), cancellationToken);
        var crvLookup = await _db.Set<CustomerReceiptVoucher>().AsNoTracking()
            .Where(v => crvIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => (v.DocumentNumber, v.ReceiptDate), cancellationToken);

        var rows = allocations.Select(a =>
        {
            string? num = null;
            DateOnly date = default;
            if (a.SupplierPaymentVoucherId is { } sId && spvLookup.TryGetValue(sId, out var sv))
            {
                num = sv.DocumentNumber;
                date = sv.PaymentDate;
            }
            else if (a.CustomerReceiptVoucherId is { } cId && crvLookup.TryGetValue(cId, out var cv))
            {
                num = cv.DocumentNumber;
                date = cv.ReceiptDate;
            }
            return new AllocationRow(
                AllocationId: a.Id,
                SupplierPaymentVoucherId: a.SupplierPaymentVoucherId,
                CustomerReceiptVoucherId: a.CustomerReceiptVoucherId,
                VoucherDocumentNumber: num,
                VoucherDate: date,
                AllocatedAmount: a.AllocatedAmount);
        }).OrderBy(r => r.VoucherDate).ToList();

        var allocatedTotal = allocations.Sum(a => a.AllocatedAmount.Amount);
        return new InvoiceAllocationView(
            InvoiceId: invoiceId,
            InvoiceType: invoiceType,
            DocumentNumber: documentNumber,
            DocumentDate: documentDate,
            GrandTotal: MoneyEgp.From(grandTotal),
            AllocatedToDate: MoneyEgp.From(allocatedTotal),
            OpenBalance: MoneyEgp.From(grandTotal - allocatedTotal),
            Allocations: rows);
    }

    private async Task<IReadOnlyList<OutstandingInvoiceRow>> BuildOutstandingRowsAsync(
        List<(Guid InvoiceId, string DocNum, DateOnly Date, Guid CounterpartyId, string CounterpartyName, decimal GrandTotal)> seed,
        CancellationToken cancellationToken)
    {
        if (seed.Count == 0)
        {
            return Array.Empty<OutstandingInvoiceRow>();
        }

        var ids = seed.Select(s => s.InvoiceId).ToArray();
        var allocatedByInvoice = await _db.Set<PaymentAllocation>().AsNoTracking()
            .Where(a => ids.Contains(a.TargetDocumentId))
            .GroupBy(a => a.TargetDocumentId)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.AllocatedAmount.Amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Sum, cancellationToken);

        return seed.Select(s =>
        {
            var allocated = allocatedByInvoice.GetValueOrDefault(s.InvoiceId, 0m);
            var open = s.GrandTotal - allocated;
            return new OutstandingInvoiceRow(
                InvoiceId: s.InvoiceId,
                DocumentNumber: s.DocNum,
                DocumentDate: s.Date,
                CounterpartyId: s.CounterpartyId,
                CounterpartyName: s.CounterpartyName,
                GrandTotal: MoneyEgp.From(s.GrandTotal),
                AllocatedToDate: MoneyEgp.From(allocated),
                OpenBalance: MoneyEgp.From(open));
        }).Where(r => r.OpenBalance.Amount > 0m).ToList();
    }
}
