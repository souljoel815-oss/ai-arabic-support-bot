namespace EgyptTax.Application.FixedAssets;

/// <summary>
/// FR-017 / US6 — operator-issued command to transition a fixed
/// asset from Draft to InService. The handler enforces the
/// FR-016-mirror attachment requirement before calling the
/// aggregate's <c>PutInService</c> mutator: capital expenditures
/// MUST have at least one supporting attachment (purchase receipt,
/// installation certificate, etc.) on file. Without it the
/// inspector has no way to validate the cost basis.
/// </summary>
public sealed record PutFixedAssetInServiceCommand(
    Guid FixedAssetId,
    Guid PutInServiceByUserId);
