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
}

public enum CustomerStatus
{
    Active,
    Inactive,
}
