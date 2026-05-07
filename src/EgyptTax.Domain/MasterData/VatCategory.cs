using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B6 — VAT-rate category. The MVP seeds <c>Standard 14%</c>; additional
/// categories (Reduced, ZeroRated, Exempt) are operator-configurable.
/// FR-022 mandates per-(code, effective_from) versioning so a rate
/// change uses the version active on the document_date — historical
/// invoices always recompute against the rate that was in force at
/// post time.
/// </summary>
public sealed class VatCategory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public decimal RatePercent { get; init; }
    public DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public bool RecoverableInputVat { get; init; }

    private VatCategory() { }

    public VatCategory(
        string code,
        ArabicEnglishText name,
        decimal ratePercent,
        DateOnly effectiveFromDate,
        DateOnly? effectiveToDate,
        bool recoverableInputVat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (ratePercent < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(ratePercent),
                "VAT rate cannot be negative.");
        }
        if (effectiveToDate is { } end && end <= effectiveFromDate)
        {
            throw new ArgumentException(
                "EffectiveToDate must be strictly after EffectiveFromDate.", nameof(effectiveToDate));
        }

        Code = code;
        Name = name;
        RatePercent = ratePercent;
        EffectiveFromDate = effectiveFromDate;
        EffectiveToDate = effectiveToDate;
        RecoverableInputVat = recoverableInputVat;
    }
}
