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

    /// <summary>
    /// FR-035 / Differentiator 1 — ETA's GS1-style item code from
    /// the regulator's master commodity list (assigned per item by
    /// the operator). Optional in the MVP because not every demo
    /// item has a real code yet; the
    /// <c>MissingEtaCodeRule</c> flags posted documents whose lines
    /// reference items without a code so the operator knows to
    /// populate it before live filing.
    /// </summary>
    public string? EtaItemCode { get; private set; }

    private Item() { }

    public Item(string code, ArabicEnglishText name, Guid defaultVatCategoryId, string? etaItemCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (defaultVatCategoryId == Guid.Empty)
        {
            throw new ArgumentException("Item must reference a default VAT category.", nameof(defaultVatCategoryId));
        }
        Code = code;
        Name = name;
        DefaultVatCategoryId = defaultVatCategoryId;
        EtaItemCode = string.IsNullOrWhiteSpace(etaItemCode) ? null : etaItemCode;
    }

    public void Deactivate() => Status = ItemStatus.Inactive;
    public void Reactivate() => Status = ItemStatus.Active;
    public void UpdateDefaultVatCategory(Guid vatCategoryId) => DefaultVatCategoryId = vatCategoryId;
    public void SetEtaItemCode(string? etaItemCode) =>
        EtaItemCode = string.IsNullOrWhiteSpace(etaItemCode) ? null : etaItemCode;
}

public enum ItemStatus
{
    Active,
    Inactive,
}
