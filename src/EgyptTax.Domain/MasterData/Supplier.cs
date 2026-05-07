using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B3 — supplier master record per FR-006 / FR-041. Mirrors the
/// shape of <see cref="Customer"/> but with the buy-side
/// <see cref="SupplierTaxProfile"/> snapshot that drives input-VAT
/// recoverability (FR-020) and reverse-charge handling. Hard delete
/// is forbidden once a posted document references the row (FR-007);
/// the status-only soft-delete keeps the historical reference
/// intact for audit purposes.
///
/// Address is the simple bilingual <see cref="ArabicEnglishText"/>
/// pair per data-model B3 — purchase invoices come FROM the supplier
/// rather than to them, so the structured-postal-address fields the
/// e-invoice JSON requires for receivers (FR-040 / Customer) are not
/// needed here.
/// </summary>
public sealed class Supplier
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public ArabicEnglishText Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public SupplierStatus Status { get; private set; } = SupplierStatus.Active;
    public SupplierTaxProfile TaxProfile { get; private set; }

    /// <summary>
    /// R-13 — wall-clock of the last successful TIN revalidation.
    /// Set by <see cref="SupplierTinRevalidationJob"/>; nullable so
    /// freshly-created rows are visible to the cron's
    /// "stale or never checked" filter.
    /// </summary>
    public DateTime? LastTinRevalidatedAtUtc { get; private set; }

    private Supplier() { }

    public Supplier(
        string code,
        ArabicEnglishText name,
        ArabicEnglishText address,
        SupplierTaxProfile taxProfile,
        string? phone = null,
        string? email = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
        TaxProfile = taxProfile;
    }

    public void UpdateAddress(ArabicEnglishText address) => Address = address;

    public void Deactivate() => Status = SupplierStatus.Inactive;
    public void Reactivate() => Status = SupplierStatus.Active;

    public void UpdateContact(string? phone, string? email)
    {
        Phone = phone;
        Email = email;
    }

    public void UpdateTaxProfile(SupplierTaxProfile profile) => TaxProfile = profile;

    public void RecordTinRevalidatedAt(DateTime utcNow) => LastTinRevalidatedAtUtc = utcNow;
}

public enum SupplierStatus
{
    Active,
    Inactive,
}
