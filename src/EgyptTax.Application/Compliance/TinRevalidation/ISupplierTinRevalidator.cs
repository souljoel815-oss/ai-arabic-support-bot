namespace EgyptTax.Application.Compliance.TinRevalidation;

/// <summary>
/// R-13 — port that hits the (mock or real) ETA TIN registry. The
/// MVP implementation is <c>AlwaysValidTinRevalidator</c>: returns
/// <see cref="TinRevalidationResult.IsValid"/> = true for every TIN.
/// The Near-term implementation will fetch the published ETA list
/// (cached locally) and validate against it.
/// </summary>
public interface ISupplierTinRevalidator
{
    Task<TinRevalidationResult> RevalidateAsync(string tin, CancellationToken cancellationToken = default);
}
