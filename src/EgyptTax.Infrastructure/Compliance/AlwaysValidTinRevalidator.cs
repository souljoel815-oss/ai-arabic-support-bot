using EgyptTax.Application.Compliance.TinRevalidation;

namespace EgyptTax.Infrastructure.Compliance;

/// <summary>
/// R-13 MVP stub — every TIN is reported valid. Production
/// behaviour is a no-op so the cron can run safely on day one
/// without a real registry feed; the Near-term task swaps this
/// for a registry-backed implementation that keeps the same port.
/// </summary>
public sealed class AlwaysValidTinRevalidator : ISupplierTinRevalidator
{
    public Task<TinRevalidationResult> RevalidateAsync(string tin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tin);
        return Task.FromResult(new TinRevalidationResult(
            Tin: tin,
            IsValid: true,
            RegistryName: "stub:always-valid",
            Reason: null));
    }
}
