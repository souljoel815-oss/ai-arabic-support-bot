using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Pricing;

/// <summary>
/// v5 B.2 — one row in a <see cref="Pricelist"/>. Either a
/// percentage discount off list (<see cref="DiscountPercent"/>)
/// OR a fixed override price (<see cref="FixedPriceEgp"/>) — the
/// constructors enforce exactly-one-of.
///
/// <see cref="ItemId"/> + <see cref="CustomerId"/> are both
/// nullable. A null ItemId means "applies to every item"; a null
/// CustomerId means "applies to every customer assigned this
/// pricelist." Combining them: a rule with both null = "10% off
/// everything for everyone on this pricelist."
///
/// <see cref="Sequence"/> orders rules within a pricelist; the
/// resolver returns the first match in sequence order so the
/// operator can put more-specific rules first.
/// </summary>
public sealed class PricelistRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PricelistId { get; init; }
    public Guid? ItemId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public decimal? FixedPriceEgp { get; private set; }
    public int Sequence { get; private set; }

    private PricelistRule() { }

    private PricelistRule(
        Guid pricelistId,
        Guid? itemId,
        Guid? customerId,
        decimal? discountPercent,
        decimal? fixedPriceEgp,
        int sequence)
    {
        if (discountPercent is not null == fixedPriceEgp is not null)
        {
            throw new ArgumentException(
                "PricelistRule must specify exactly one of DiscountPercent OR FixedPriceEgp.");
        }
        if (discountPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent),
                "Discount percent must be 0–100.");
        }
        if (fixedPriceEgp is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedPriceEgp),
                "Fixed price cannot be negative.");
        }
        PricelistId = pricelistId;
        ItemId = itemId == Guid.Empty ? null : itemId;
        CustomerId = customerId == Guid.Empty ? null : customerId;
        DiscountPercent = discountPercent;
        FixedPriceEgp = fixedPriceEgp;
        Sequence = sequence;
    }

    public static PricelistRule CreateDiscount(
        Guid pricelistId, Guid? itemId, Guid? customerId, decimal discountPercent, int sequence) =>
        new(pricelistId, itemId, customerId, discountPercent, null, sequence);

    public static PricelistRule CreateFixedPrice(
        Guid pricelistId, Guid? itemId, Guid? customerId, MoneyEgp fixedPrice, int sequence) =>
        new(pricelistId, itemId, customerId, null, fixedPrice.Amount, sequence);

    /// <summary>True iff this rule applies for the given item +
    /// customer combination. A rule's null fields are wildcards.</summary>
    public bool Matches(Guid itemId, Guid customerId) =>
        (ItemId is null || ItemId == itemId)
        && (CustomerId is null || CustomerId == customerId);

    /// <summary>Apply this rule to a list price. Returns the
    /// resolved unit price in EGP.</summary>
    public decimal Apply(decimal listPriceEgp)
    {
        if (FixedPriceEgp is decimal fp) return fp;
        if (DiscountPercent is decimal dp)
            return decimal.Round(listPriceEgp * (1m - dp / 100m), 4, MidpointRounding.ToEven);
        // Should be unreachable by the ctor invariant.
        return listPriceEgp;
    }
}
