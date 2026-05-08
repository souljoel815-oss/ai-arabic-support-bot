using EgyptTax.Domain.Documents;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.FixedAssets;

/// <summary>
/// FR-017 / FR-018 / T187 — read surface for the fixed-asset
/// pages. Lists every asset (Draft / InService / Disposed /
/// WrittenOff) with the FR-018 net book value computed for an
/// as-of date so the list page can show what each asset is worth
/// today; the detail/schedule page pulls the full schedule from
/// <see cref="DepreciationEngine"/> directly.
/// </summary>
public interface IFixedAssetQuery
{
    Task<IReadOnlyList<FixedAssetListRow>> ListAsync(
        DateOnly asOf, CancellationToken cancellationToken = default);

    Task<FixedAsset?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record FixedAssetListRow(
    Guid Id,
    string Code,
    ArabicEnglishText Description,
    string AssetCategory,
    FixedAssetStatus Status,
    MoneyEgp Cost,
    DateOnly InServiceDate,
    int UsefulLifeMonths,
    DepreciationConvention Convention,
    MoneyEgp NetBookValue,
    MoneyEgp DepreciatedToDate,
    int AttachmentCount);
