using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Compliance;

/// <summary>
/// T142 — EF-backed implementation of
/// <see cref="IPurchaseInvoiceFingerprintQuery"/>. Hits the
/// `ix_purchase_invoices_supplier_dedup` covering index so the
/// dedup lookup stays seek-friendly even when a single supplier has
/// thousands of invoices on file.
/// </summary>
public sealed class SqlPurchaseInvoiceFingerprintQuery : IPurchaseInvoiceFingerprintQuery
{
    private readonly AppDbContext _db;

    public SqlPurchaseInvoiceFingerprintQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PurchaseInvoiceFingerprint>>
        FindOtherPurchaseInvoicesWithSameSupplierReferenceAsync(
            Guid supplierId,
            string supplierInvoiceNumber,
            Guid? excludePurchaseInvoiceId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierInvoiceNumber);

        var query = _db.Set<PurchaseInvoice>()
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId
                && p.SupplierInvoiceNumber == supplierInvoiceNumber);

        if (excludePurchaseInvoiceId is { } excludeId)
        {
            query = query.Where(p => p.Id != excludeId);
        }

        var rows = await query
            .Select(p => new
            {
                p.Id,
                p.DocumentNumber,
                p.SupplierInvoiceNumber,
                p.DateReceived,
                GrandTotalAmount = p.GrandTotal.Amount,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new PurchaseInvoiceFingerprint(
                r.Id, r.DocumentNumber, r.SupplierInvoiceNumber,
                r.DateReceived, MoneyEgp.From(r.GrandTotalAmount)))
            .ToList();
    }
}
