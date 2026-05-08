using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B7 / FR-045 — withholding-tax classification per service /
/// payment type with effective-from-dated rate. Mirrors
/// <see cref="VatCategory"/> on the VAT side; the operator config
/// surface (US7's WhtCategories.razor) lets an Administrator
/// supersede a category by inserting a new row with a later
/// `effective_from_date`. R-17 — WHT compute uses the category in
/// force on the **payment date** (not the invoice date — Egyptian
/// WHT is event-dated to the cash flow).
/// </summary>
public sealed class WhtCategory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public decimal RatePercent { get; init; }
    public DateOnly EffectiveFromDate { get; init; }
    public DateOnly? EffectiveToDate { get; init; }
    public WhtApplicableTo ApplicableTo { get; init; }

    private WhtCategory() { }

    public WhtCategory(
        string code,
        ArabicEnglishText name,
        decimal ratePercent,
        DateOnly effectiveFromDate,
        DateOnly? effectiveToDate,
        WhtApplicableTo applicableTo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (ratePercent < 0m || ratePercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(ratePercent),
                "WHT rate must be in the range [0, 100] percent.");
        }
        if (effectiveToDate is { } end && end < effectiveFromDate)
        {
            throw new ArgumentException(
                $"EffectiveToDate {end:yyyy-MM-dd} cannot precede EffectiveFromDate {effectiveFromDate:yyyy-MM-dd}.",
                nameof(effectiveToDate));
        }

        Code = code;
        Name = name;
        RatePercent = ratePercent;
        EffectiveFromDate = effectiveFromDate;
        EffectiveToDate = effectiveToDate;
        ApplicableTo = applicableTo;
    }

    /// <summary>True when the supplied date falls inside this
    /// category's effective window. Both bounds inclusive; null
    /// upper bound means "open-ended".</summary>
    public bool IsEffectiveOn(DateOnly date) =>
        date >= EffectiveFromDate
        && (EffectiveToDate is null || date <= EffectiveToDate.Value);
}

/// <summary>FR-045 — which side(s) of a payment a WHT category
/// applies to. SuppliersServices = the company withholds from a
/// supplier-services payment (outbound certificate); CustomersServices
/// = the customer withholds from a payment to the company
/// (inbound certificate). Both = applicable on either side.</summary>
public enum WhtApplicableTo
{
    SuppliersServices,
    CustomersServices,
    Both,
}
