namespace EgyptTax.Application.Common.Guards;

/// <summary>
/// T140 / FR-007 — port that asks "is this master-data row safe to
/// hard-delete?". The MVP UI exposes only deactivate/reactivate
/// (status-flag soft delete), so this guard's primary callers are
/// future admin tools + data-migration scripts. Each method returns
/// a structured <see cref="MasterDataReferenceCheck"/> so the caller
/// can render a specific "blocked because N posted invoices
/// reference it" message rather than a generic rejection.
/// </summary>
public interface IMasterDataDeletionGuard
{
    Task<MasterDataReferenceCheck> CheckSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default
    );
    Task<MasterDataReferenceCheck> CheckCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default
    );
    Task<MasterDataReferenceCheck> CheckItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default
    );
    Task<MasterDataReferenceCheck> CheckExpenseCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default
    );
    Task<MasterDataReferenceCheck> CheckVatCategoryAsync(
        Guid vatCategoryId,
        CancellationToken cancellationToken = default
    );
}
