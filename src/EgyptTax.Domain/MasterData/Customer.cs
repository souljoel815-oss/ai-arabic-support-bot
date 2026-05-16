using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B2 — customer master record. Hard delete is forbidden once a posted
/// document references the row (enforced by the FK + status flag); the
/// status-only soft-delete keeps the historical reference intact for
/// audit purposes.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public PostalAddress Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;
    public CustomerTaxProfile TaxProfile { get; private set; }

    /// <summary>
    /// Phase C — credit limit in EGP. Null = no limit (default for
    /// existing customers). When set, the post-time guard rejects
    /// new sales that would push the customer's outstanding balance
    /// above this ceiling. Computed as posted-not-paid receivable;
    /// see <c>CustomerBalanceQuery</c>.
    /// </summary>
    public decimal? CreditLimitEgp { get; private set; }

    /// <summary>v5 B.2 — default Pricelist for this customer.
    /// Null = standard <c>Item.UnitPrice</c> applies. When set,
    /// the <c>PricelistResolver</c> picks the first matching rule
    /// at invoice-line-add time and pre-fills the unit price.</summary>
    public Guid? DefaultPricelistId { get; private set; }

    /// <summary>v5 F.4 — fiscal position controlling tax mapping
    /// behaviour for this customer's invoices. Null = no special
    /// treatment (default VAT category from the line's item).
    /// When set, <c>FiscalPositionResolver</c> swaps the line's
    /// VAT category at invoice-line-add time per the position's
    /// mappings (e.g. exporters → 0% VAT, free zone → exempt).</summary>
    public Guid? FiscalPositionId { get; private set; }

    private Customer() { }

    public Customer(
        string code,
        ArabicEnglishText name,
        PostalAddress address,
        CustomerTaxProfile taxProfile,
        string? phone = null,
        string? email = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
        TaxProfile = taxProfile;
    }

    public void UpdateAddress(PostalAddress address) => Address = address;

    public void Deactivate() => Status = CustomerStatus.Inactive;

    public void Reactivate() => Status = CustomerStatus.Active;

    public void UpdateContact(string? phone, string? email)
    {
        Phone = phone;
        Email = email;
    }

    public void UpdateTaxProfile(CustomerTaxProfile profile) => TaxProfile = profile;

    public void UpdateCreditLimit(decimal? limitEgp)
    {
        if (limitEgp is < 0)
            throw new ArgumentOutOfRangeException(nameof(limitEgp),
                "Credit limit cannot be negative.");
        CreditLimitEgp = limitEgp;
    }

    /// <summary>v5 B.2 — assign default pricelist or detach (null).
    /// The application layer enforces that the pricelist exists +
    /// is Active before calling.</summary>
    public void AssignPricelist(Guid? pricelistId)
    {
        if (pricelistId == Guid.Empty) pricelistId = null;
        DefaultPricelistId = pricelistId;
    }

    /// <summary>v5 F.4 — assign or detach the fiscal position.
    /// The application layer enforces existence + active state.</summary>
    public void AssignFiscalPosition(Guid? fiscalPositionId)
    {
        if (fiscalPositionId == Guid.Empty) fiscalPositionId = null;
        FiscalPositionId = fiscalPositionId;
    }
}

public enum CustomerStatus
{
    Active,
    Inactive,
}
