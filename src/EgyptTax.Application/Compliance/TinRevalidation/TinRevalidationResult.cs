namespace EgyptTax.Application.Compliance.TinRevalidation;

/// <summary>
/// Outcome of one TIN revalidation call against the (mock or real)
/// ETA registry. Per R-13, the MVP ships the cron skeleton + audit
/// path; the remote-list integration plugs in later as a
/// Near-term task by replacing the
/// <see cref="ISupplierTinRevalidator"/> implementation.
/// </summary>
public sealed record TinRevalidationResult(
    string Tin,
    bool IsValid,
    string? RegistryName,
    string? Reason);
