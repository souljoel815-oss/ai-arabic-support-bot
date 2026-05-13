using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// L4 (v3 roadmap) — stock location / warehouse. Egyptian SMBs
/// typically have one or two: "الفرع" (the shop) + "المخزن
/// الكبير" (the back-storage warehouse). Multi-warehouse here is
/// scoped tightly — per-location stock counters + transfers
/// between locations. NOT putaway strategies, NOT
/// in-transit states, NOT bin locations within a warehouse.
///
/// v1 ships the schema + locations management page WITHOUT
/// touching the sales-posting path. Existing
/// <c>Item.QuantityOnHand</c> remains authoritative; new
/// per-location stock rows live alongside it. The posting path
/// migration is a follow-on once a real customer asks for it.
/// </summary>
public sealed class StockLocation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;

    private StockLocation() { }

    public StockLocation(string code, ArabicEnglishText name, bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
        Name = name;
        IsDefault = isDefault;
    }

    public void Rename(ArabicEnglishText newName) => Name = newName;
    public void MarkDefault() => IsDefault = true;
    public void UnmarkDefault() => IsDefault = false;
    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
