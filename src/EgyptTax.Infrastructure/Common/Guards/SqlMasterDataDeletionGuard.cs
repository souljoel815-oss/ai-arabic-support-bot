using EgyptTax.Application.Common.Guards;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Common.Guards;

/// <summary>
/// T140 / FR-007 — EF-backed implementation. One concise query per
/// referencing-document-type using the existing FK indexes
/// (`ix_*_supplier_id`, `ix_*_customer_id`, etc.) so the guard
/// stays seek-friendly even on large datasets.
///
/// Counts include BOTH draft and posted documents — the deletion
/// of a row referenced by even a draft would orphan that draft's
/// foreign key. The caller-side message can disambiguate posted
/// vs draft by inspecting the numbers if needed; the guard itself
/// keeps the contract simple ("any reference is a block").
/// </summary>
public sealed class SqlMasterDataDeletionGuard : IMasterDataDeletionGuard
{
    private readonly AppDbContext _db;

    public SqlMasterDataDeletionGuard(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MasterDataReferenceCheck> CheckSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default
    )
    {
        var purchaseCount = await _db.Set<PurchaseInvoice>()
            .CountAsync(p => p.SupplierId == supplierId, cancellationToken);
        return Decide(("PurchaseInvoice", purchaseCount));
    }

    public async Task<MasterDataReferenceCheck> CheckCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default
    )
    {
        var salesCount = await _db.Set<SalesInvoice>()
            .CountAsync(s => s.CustomerId == customerId, cancellationToken);
        return Decide(("SalesInvoice", salesCount));
    }

    public async Task<MasterDataReferenceCheck> CheckItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default
    )
    {
        // Item lines live on both sales and purchase docs; both
        // reference the row, both block deletion.
        var salesLineCount = await _db.Set<SalesInvoiceLine>()
            .CountAsync(l => l.ItemId == itemId, cancellationToken);
        var purchaseLineCount = await _db.Set<PurchaseInvoiceLine>()
            .CountAsync(l => l.ItemId == itemId, cancellationToken);
        return Decide(
            ("SalesInvoiceLine", salesLineCount),
            ("PurchaseInvoiceLine", purchaseLineCount)
        );
    }

    public async Task<MasterDataReferenceCheck> CheckExpenseCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default
    )
    {
        var expenseCount = await _db.Set<Expense>()
            .CountAsync(e => e.CategoryId == categoryId, cancellationToken);
        var purchaseLineCount = await _db.Set<PurchaseInvoiceLine>()
            .CountAsync(l => l.ExpenseCategoryId == categoryId, cancellationToken);
        return Decide(("Expense", expenseCount), ("PurchaseInvoiceLine", purchaseLineCount));
    }

    public async Task<MasterDataReferenceCheck> CheckVatCategoryAsync(
        Guid vatCategoryId,
        CancellationToken cancellationToken = default
    )
    {
        var salesLineCount = await _db.Set<SalesInvoiceLine>()
            .CountAsync(l => l.VatCategoryId == vatCategoryId, cancellationToken);
        var purchaseLineCount = await _db.Set<PurchaseInvoiceLine>()
            .CountAsync(l => l.VatCategoryId == vatCategoryId, cancellationToken);
        return Decide(
            ("SalesInvoiceLine", salesLineCount),
            ("PurchaseInvoiceLine", purchaseLineCount)
        );
    }

    private static MasterDataReferenceCheck Decide(params (string Type, int Count)[] counts)
    {
        var nonZero = counts
            .Where(c => c.Count > 0)
            .Select(c => new MasterDataReference(c.Type, c.Count))
            .ToList();
        return nonZero.Count == 0
            ? MasterDataReferenceCheck.Allowed
            : MasterDataReferenceCheck.Blocked(nonZero);
    }
}
