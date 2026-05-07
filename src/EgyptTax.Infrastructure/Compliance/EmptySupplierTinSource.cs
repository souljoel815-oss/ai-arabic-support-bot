using EgyptTax.Application.Compliance.TinRevalidation;

namespace EgyptTax.Infrastructure.Compliance;

/// <summary>
/// US1-time stub — there is no Supplier aggregate yet (US2 ships
/// it), so the cron has nothing to iterate. Returns an empty list;
/// the US2 batch replaces this with an EF-backed implementation
/// that queries the suppliers table for the rows whose
/// <c>last_revalidated_at</c> is older than the cron interval.
/// </summary>
public sealed class EmptySupplierTinSource : ISupplierTinSource
{
    private static readonly IReadOnlyList<SupplierTinRow> Empty = Array.Empty<SupplierTinRow>();

    public Task<IReadOnlyList<SupplierTinRow>> GetSuppliersToRevalidateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Empty);
}
