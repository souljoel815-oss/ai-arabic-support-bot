using EgyptTax.Domain.Documents;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.FixedAssets;

/// <summary>
/// FR-017 / T187 — operator creates a Draft FixedAsset.
/// Persistence + validation happen in the matching handler. Once
/// the operator has attached the supporting documentation,
/// <see cref="PutFixedAssetInServiceCommand"/> transitions Draft
/// → InService (FR-016 mirror checks the attachment).
/// </summary>
public sealed record CreateFixedAssetCommand(
    string Code,
    ArabicEnglishText Description,
    string AssetCategory,
    MoneyEgp Cost,
    DateOnly InServiceDate,
    int UsefulLifeMonths,
    DepreciationMethod DepreciationMethod,
    MoneyEgp SalvageValue,
    DepreciationConvention Convention,
    Guid CreatedByUserId);
