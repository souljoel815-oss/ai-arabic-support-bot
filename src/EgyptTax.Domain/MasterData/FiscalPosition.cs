using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v5 F.4 — fiscal position groups customers by tax behaviour:
/// Egyptian exports get 0% VAT, free-zone customers get exempt,
/// GCC customers might map to a different VAT category. Today the
/// operator manually overrides VAT on each export-invoice line —
/// error-prone and easy to miss.
///
/// MVP scope:
///   * Master entity + Customer.FiscalPositionId FK
///   * The actual auto-swap of VAT category on invoice-line
///     creation lands in a follow-up pass (touches every monetary
///     calculation; staged behind a feature flag in the receipt
///     post path to keep the auto-emitter clean).
///
/// MappingRules let an operator say "any line that would normally
/// post 14% Standard VAT routes to Export-0% instead." Empty
/// destination means "exempt — no VAT line at all."
/// </summary>
public sealed class FiscalPosition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ArabicEnglishText Name { get; private set; }
    public string? Description { get; private set; }
    public string? CountryAutoApply { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; init; }

    private readonly List<FiscalPositionMapping> _mappings = new();
    public IReadOnlyCollection<FiscalPositionMapping> Mappings => _mappings;

    private FiscalPosition()
    {
        Name = new ArabicEnglishText("", "");
    }

    public FiscalPosition(
        ArabicEnglishText name,
        string? description,
        string? countryAutoApply,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CountryAutoApply = string.IsNullOrWhiteSpace(countryAutoApply) ? null : countryAutoApply.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(ArabicEnglishText name, string? description, string? countryAutoApply)
    {
        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CountryAutoApply = string.IsNullOrWhiteSpace(countryAutoApply) ? null : countryAutoApply.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    public FiscalPositionMapping AddMapping(Guid sourceVatCategoryId, Guid? destinationVatCategoryId)
    {
        var m = new FiscalPositionMapping(Id, sourceVatCategoryId, destinationVatCategoryId);
        _mappings.Add(m);
        return m;
    }

    public void RemoveAllMappings() => _mappings.Clear();
}

public sealed class FiscalPositionMapping
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid FiscalPositionId { get; init; }
    public Guid SourceVatCategoryId { get; init; }
    /// <summary>Null = exempt (no VAT line at all on the destination invoice line).</summary>
    public Guid? DestinationVatCategoryId { get; init; }

    private FiscalPositionMapping() { }

    public FiscalPositionMapping(Guid fiscalPositionId, Guid sourceVatCategoryId, Guid? destinationVatCategoryId)
    {
        if (sourceVatCategoryId == Guid.Empty)
        {
            throw new ArgumentException("SourceVatCategoryId required.", nameof(sourceVatCategoryId));
        }
        FiscalPositionId = fiscalPositionId;
        SourceVatCategoryId = sourceVatCategoryId;
        DestinationVatCategoryId = destinationVatCategoryId;
    }
}
