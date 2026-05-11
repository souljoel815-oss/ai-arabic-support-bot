namespace EgyptTax.Application.Eta;

/// <summary>
/// P1.6 — port over the two registries that hand out ETA-acceptable
/// item codes:
///
///   * <c>GS1 Egypt</c> — fast (24-48h cache propagation) but
///     requires the operator to register a GTIN with GS1 first
///     (separate paid step outside the app).
///   * <c>EGS</c> — direct via ETA's commodity service, slower
///     (~15 days approval) but no GS1 dependency.
///
/// The check job calls <see cref="LookupAsync"/> for every Pending
/// item past the registry's expected SLA window and applies the
/// returned outcome to the row. Mock implementation simulates both
/// registries with deterministic outcomes; production wires real
/// HTTP clients.
/// </summary>
public interface IEtaItemCodeRegistry
{
    /// <summary>
    /// Look up the current registry state for an item. Returns
    /// <c>StillPending</c> when the registry hasn't finished
    /// processing; <c>Active</c> with an issued code when the
    /// registry confirms the SKU; <c>Failed</c> with a reason when
    /// the registry rejected the request.
    /// </summary>
    Task<EtaItemCodeLookupResult> LookupAsync(
        Guid itemId,
        EgyptTax.Domain.MasterData.EtaItemCodeKind kind,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record EtaItemCodeLookupResult(
    EtaItemCodeLookupStatus Status,
    string? IssuedCode,
    string? FailureReason);

public enum EtaItemCodeLookupStatus
{
    StillPending,
    Active,
    Failed,
}
