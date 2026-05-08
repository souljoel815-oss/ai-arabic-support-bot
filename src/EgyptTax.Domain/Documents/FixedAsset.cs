using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Documents;

/// <summary>
/// C4 / FR-017 / FR-018 / US6 — capitalized fixed asset. Tracks
/// the cost basis + depreciation parameters at acquisition; the
/// <see cref="EgyptTax.Application.FixedAssets.DepreciationEngine"/>
/// is responsible for computing per-period depreciation amounts +
/// the running net book value.
///
/// State machine: Draft → InService (one-way; requires an
/// attachment per FR-016 mirror — the application handler
/// enforces) → Disposed | WrittenOff (terminal). Depreciation runs
/// only for InService assets; disposal/write-off freezes the
/// schedule at the disposal date.
///
/// MVP scope: straight-line method only (FR-017 Phase 4 minimum);
/// FullMonth + MidMonth conventions (the two an Egyptian
/// accountant reaches for first). Other methods + conventions slot
/// in via the <see cref="DepreciationMethod"/> + <see cref="Convention"/>
/// enums when the operator config surface lands.
/// </summary>
public sealed class FixedAsset
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Description { get; private set; }
    public string AssetCategory { get; private set; } = "";
    public MoneyEgp Cost { get; private set; }
    public DateOnly InServiceDate { get; private set; }
    public int UsefulLifeMonths { get; private set; }
    public DepreciationMethod DepreciationMethod { get; private set; }
    public MoneyEgp SalvageValue { get; private set; }
    public DepreciationConvention Convention { get; private set; }
    public FixedAssetStatus Status { get; private set; } = FixedAssetStatus.Draft;
    public DateOnly? DisposedOn { get; private set; }

    private FixedAsset()
    {
        Description = new ArabicEnglishText("", "");
    }

    private FixedAsset(
        string code,
        ArabicEnglishText description,
        string assetCategory,
        MoneyEgp cost,
        DateOnly inServiceDate,
        int usefulLifeMonths,
        DepreciationMethod depreciationMethod,
        MoneyEgp salvageValue,
        DepreciationConvention convention
    )
    {
        Code = code;
        Description = description;
        AssetCategory = assetCategory;
        Cost = cost;
        InServiceDate = inServiceDate;
        UsefulLifeMonths = usefulLifeMonths;
        DepreciationMethod = depreciationMethod;
        SalvageValue = salvageValue;
        Convention = convention;
    }

    /// <summary>
    /// Factory for a new draft asset. Validates the parameters that
    /// can be checked structurally (positive useful life, salvage
    /// non-negative + ≤ cost); the FR-016 attachment requirement is
    /// a handler-side check at PutInService time because it requires
    /// a DB lookup.
    /// </summary>
    public static FixedAsset CreateDraft(
        string code,
        ArabicEnglishText description,
        string assetCategory,
        MoneyEgp cost,
        DateOnly inServiceDate,
        int usefulLifeMonths,
        DepreciationMethod depreciationMethod,
        MoneyEgp salvageValue,
        DepreciationConvention convention
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetCategory);
        if (cost.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cost),
                "Fixed-asset cost must be positive."
            );
        }
        if (usefulLifeMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usefulLifeMonths),
                "Useful life must be at least 1 month."
            );
        }
        if (salvageValue.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(salvageValue),
                "Salvage value cannot be negative."
            );
        }
        if (salvageValue.Amount > cost.Amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(salvageValue),
                $"Salvage value {salvageValue.Amount:F2} cannot exceed cost {cost.Amount:F2}."
            );
        }
        return new FixedAsset(
            code,
            description,
            assetCategory,
            cost,
            inServiceDate,
            usefulLifeMonths,
            depreciationMethod,
            salvageValue,
            convention
        );
    }

    /// <summary>FR-017 / US6 — transitions Draft → InService. The
    /// application handler enforces the FR-016-mirror attachment
    /// requirement (deductible capital expenditure needs supporting
    /// documents) BEFORE calling this method.</summary>
    public void PutInService()
    {
        if (Status != FixedAssetStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot put fixed asset {Id} in service: current status is {Status}, not Draft."
            );
        }
        Status = FixedAssetStatus.InService;
    }

    /// <summary>Mark the asset as disposed (sold or otherwise removed
    /// from the books at <paramref name="disposalDate"/>). Depreciation
    /// stops at this date; subsequent schedule queries return zero
    /// amounts past it.</summary>
    public void Dispose(DateOnly disposalDate)
    {
        if (Status != FixedAssetStatus.InService)
        {
            throw new InvalidOperationException(
                $"Cannot dispose fixed asset {Id}: current status is {Status}, not InService."
            );
        }
        if (disposalDate < InServiceDate)
        {
            throw new ArgumentException(
                $"Disposal date {disposalDate:yyyy-MM-dd} cannot precede in-service date {InServiceDate:yyyy-MM-dd}.",
                nameof(disposalDate)
            );
        }
        Status = FixedAssetStatus.Disposed;
        DisposedOn = disposalDate;
    }

    /// <summary>Mark the asset as written off (impairment / loss).
    /// Same depreciation-stop semantics as <see cref="Dispose"/>.</summary>
    public void WriteOff(DateOnly writeOffDate)
    {
        if (Status != FixedAssetStatus.InService)
        {
            throw new InvalidOperationException(
                $"Cannot write off fixed asset {Id}: current status is {Status}, not InService."
            );
        }
        if (writeOffDate < InServiceDate)
        {
            throw new ArgumentException(
                $"Write-off date {writeOffDate:yyyy-MM-dd} cannot precede in-service date {InServiceDate:yyyy-MM-dd}.",
                nameof(writeOffDate)
            );
        }
        Status = FixedAssetStatus.WrittenOff;
        DisposedOn = writeOffDate;
    }
}

public enum FixedAssetStatus
{
    Draft,
    InService,
    Disposed,
    WrittenOff,
}

public enum DepreciationMethod
{
    StraightLine,
}

public enum DepreciationConvention
{
    /// <summary>The asset gets a full month's depreciation in its
    /// in-service month regardless of the day. Simplest convention
    /// + the Egyptian SME default.</summary>
    FullMonth,

    /// <summary>The asset gets a half-month's depreciation in its
    /// in-service month and a half-month in its disposal month
    /// (US6 scenario 2 — asset placed in service on the 15th).</summary>
    MidMonth,
}
