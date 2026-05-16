namespace EgyptTax.Application.Inventory;

/// <summary>v5 F.5 — transition a Draft <c>LandedCost</c> to
/// Validated. Allocates a <c>LC-{year}-{n}</c> document number,
/// emits the balanced JE (DR Inventory per allocation / CR each
/// clearing account per cost line). Allocation rows must already
/// be present on the aggregate (the page calls Compute before
/// firing this command).</summary>
public sealed record ValidateLandedCostCommand(
    Guid LandedCostId,
    Guid ValidatedByUserId);
