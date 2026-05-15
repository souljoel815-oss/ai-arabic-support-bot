using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Pricing;

/// <summary>
/// v5 B.2 — customer-specific pricing. A <see cref="Pricelist"/>
/// is a named bundle of <see cref="PricelistRule"/> rows; a
/// <c>Customer.DefaultPricelistId</c> opts that customer into the
/// pricelist's rules. On invoice-line add the system resolves the
/// unit price as: pricelist rules first match → fallback to
/// <c>Item.UnitPrice</c>.
///
/// Out of scope per the v5 spec: volume-based tiers, date-bound
/// validity windows, pricelist-on-pricelist inheritance, currency-
/// specific rules. Add only if a customer asks.
/// </summary>
public sealed class Pricelist
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ArabicEnglishText Name { get; private set; }
    public PricelistStatus Status { get; private set; } = PricelistStatus.Active;
    public DateTime CreatedAtUtc { get; init; }

    private readonly List<PricelistRule> _rules = new();
    public IReadOnlyCollection<PricelistRule> Rules => _rules;

    private Pricelist() { }

    public Pricelist(ArabicEnglishText name, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name.Arabic) && string.IsNullOrWhiteSpace(name.English))
        {
            throw new ArgumentException("Pricelist name (Ar or En) is required.", nameof(name));
        }
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public void Rename(ArabicEnglishText name)
    {
        if (string.IsNullOrWhiteSpace(name.Arabic) && string.IsNullOrWhiteSpace(name.English))
        {
            throw new ArgumentException("Pricelist name (Ar or En) is required.", nameof(name));
        }
        Name = name;
    }

    public void Deactivate() => Status = PricelistStatus.Inactive;
    public void Reactivate() => Status = PricelistStatus.Active;

    public PricelistRule AddDiscountRule(Guid? itemId, Guid? customerId, decimal discountPercent)
    {
        var rule = PricelistRule.CreateDiscount(Id, itemId, customerId, discountPercent, _rules.Count);
        _rules.Add(rule);
        return rule;
    }

    public PricelistRule AddFixedPriceRule(Guid? itemId, Guid? customerId, MoneyEgp fixedPrice)
    {
        var rule = PricelistRule.CreateFixedPrice(Id, itemId, customerId, fixedPrice, _rules.Count);
        _rules.Add(rule);
        return rule;
    }

    public void RemoveRule(Guid ruleId)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is not null) _rules.Remove(rule);
    }
}

public enum PricelistStatus
{
    Active,
    Inactive,
}
