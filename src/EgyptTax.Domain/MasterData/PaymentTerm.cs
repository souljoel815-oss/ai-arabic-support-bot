using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v5 E.8 — payment term with optional early-settlement discount.
/// Replaces the single-number <c>InvoiceSettings.DefaultPaymentTermsDays</c>
/// with a master-data entity that captures "Net N" plus the
/// "2/10 Net 30" style early-pay incentive Egyptian wholesalers
/// routinely offer.
///
/// Examples:
///   • Net 30:        NetDays=30, DiscountPercent=0,  DiscountWindowDays=0
///   • 2/10 Net 30:   NetDays=30, DiscountPercent=2,  DiscountWindowDays=10
///   • 5/7 Net 45:    NetDays=45, DiscountPercent=5,  DiscountWindowDays=7
/// </summary>
public sealed class PaymentTerm
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ArabicEnglishText Name { get; private set; }
    public int NetDays { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public int DiscountWindowDays { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; init; }

    private PaymentTerm()
    {
        Name = new ArabicEnglishText("", "");
    }

    public PaymentTerm(
        ArabicEnglishText name,
        int netDays,
        decimal discountPercent,
        int discountWindowDays,
        bool isDefault,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (netDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(netDays), "Net days cannot be negative.");
        }
        if (discountPercent < 0m || discountPercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent),
                "Discount percent must be between 0 and 100.");
        }
        if (discountWindowDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discountWindowDays),
                "Discount window days cannot be negative.");
        }
        if (discountWindowDays > netDays)
        {
            throw new ArgumentException(
                $"Discount window ({discountWindowDays}d) cannot exceed net days ({netDays}d).");
        }
        if ((discountPercent > 0m) != (discountWindowDays > 0))
        {
            throw new ArgumentException(
                "Discount percent and discount window must be both > 0 or both 0 — half-filled terms make no sense.");
        }
        Name = name;
        NetDays = netDays;
        DiscountPercent = discountPercent;
        DiscountWindowDays = discountWindowDays;
        IsDefault = isDefault;
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(
        ArabicEnglishText name,
        int netDays,
        decimal discountPercent,
        int discountWindowDays,
        bool isDefault)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(netDays);
        if (discountPercent < 0m || discountPercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent),
                "Discount percent must be between 0 and 100.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(discountWindowDays);
        if (discountWindowDays > netDays)
        {
            throw new ArgumentException(
                $"Discount window ({discountWindowDays}d) cannot exceed net days ({netDays}d).");
        }
        if ((discountPercent > 0m) != (discountWindowDays > 0))
        {
            throw new ArgumentException("Discount % and window must agree.");
        }
        Name = name;
        NetDays = netDays;
        DiscountPercent = discountPercent;
        DiscountWindowDays = discountWindowDays;
        IsDefault = isDefault;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
    public void MarkDefault() => IsDefault = true;
    public void UnmarkDefault() => IsDefault = false;

    /// <summary>
    /// Returns the discount amount earned if the gross invoice is
    /// paid on the given date. Zero when the term carries no
    /// discount, or when the payment date falls past the
    /// discount window.
    /// </summary>
    public MoneyEgp EarnedDiscount(MoneyEgp gross, DateOnly invoiceDate, DateOnly paymentDate)
    {
        if (DiscountPercent == 0m || DiscountWindowDays == 0) return MoneyEgp.Zero;
        var deadline = invoiceDate.AddDays(DiscountWindowDays);
        if (paymentDate > deadline) return MoneyEgp.Zero;
        var amount = decimal.Round(gross.Amount * (DiscountPercent / 100m), 2, MidpointRounding.ToEven);
        return MoneyEgp.From(amount);
    }
}
