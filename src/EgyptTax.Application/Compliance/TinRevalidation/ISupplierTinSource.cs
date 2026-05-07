namespace EgyptTax.Application.Compliance.TinRevalidation;

/// <summary>
/// Port returning the set of supplier TINs the cron should
/// revalidate on each tick. Decoupled from the Supplier aggregate
/// so the US1-time stub can return an empty set without forcing
/// the Supplier entity (US2 territory) to ship early. When US2
/// lands, the EF-backed implementation iterates registered
/// suppliers from the master-data tables.
/// </summary>
public interface ISupplierTinSource
{
    Task<IReadOnlyList<SupplierTinRow>> GetSuppliersToRevalidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight projection — just what the cron needs to issue an
/// audit row keyed back to the supplier. Avoids dragging the
/// Supplier aggregate into Application before it exists.
/// </summary>
public sealed record SupplierTinRow(Guid SupplierId, string Tin, string DisplayName);
