using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B4 — item master record. The chart-of-account FKs
/// (<c>default_revenue_account_id</c>, <c>default_expense_account_id</c>)
/// referenced in data-model B4 are deferred until US5 introduces the
/// chart of accounts; the MVP slice uses the VAT category alone to
/// drive line tax calculation.
/// </summary>
public sealed class Item
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public Guid DefaultVatCategoryId { get; private set; }
    public ItemStatus Status { get; private set; } = ItemStatus.Active;

    private Item() { }

    public Item(string code, ArabicEnglishText name, Guid defaultVatCategoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (defaultVatCategoryId == Guid.Empty)
        {
            throw new ArgumentException("Item must reference a default VAT category.", nameof(defaultVatCategoryId));
        }
        Code = code;
        Name = name;
        DefaultVatCategoryId = defaultVatCategoryId;
    }

    public void Deactivate() => Status = ItemStatus.Inactive;
    public void Reactivate() => Status = ItemStatus.Active;
    public void UpdateDefaultVatCategory(Guid vatCategoryId) => DefaultVatCategoryId = vatCategoryId;
}

public enum ItemStatus
{
    Active,
    Inactive,
}
