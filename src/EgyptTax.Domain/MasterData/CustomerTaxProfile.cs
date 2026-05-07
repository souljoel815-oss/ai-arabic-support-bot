using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B2a — value object embedded on <see cref="Customer"/> per FR-040.
/// Snapshotted onto the SalesInvoice header at post-time so audit
/// integrity survives master-data corrections later. The factory
/// methods enforce the data-model invariant
/// <c>tin non-null iff profile_type = B2BRegistered</c>. Persistence
/// stores the TIN as a raw 9-digit string for clean EF mapping; the
/// <see cref="Tin"/> property re-parses through <see cref="EgyptianTin"/>
/// for typed access.
/// </summary>
public readonly record struct CustomerTaxProfile(
    CustomerTaxProfileType ProfileType,
    string? TinValue,
    bool VatExemption,
    Guid? DefaultSalesVatCategoryId)
{
    public EgyptianTin? Tin =>
        string.IsNullOrEmpty(TinValue)
            ? null
            : EgyptianTin.Parse(TinValue);

    public static CustomerTaxProfile B2BRegistered(EgyptianTin tin, bool vatExemption, Guid? defaultSalesVatCategoryId) =>
        new(CustomerTaxProfileType.B2BRegistered, tin.Value, vatExemption, defaultSalesVatCategoryId);

    public static CustomerTaxProfile B2BUnregistered(bool vatExemption, Guid? defaultSalesVatCategoryId) =>
        new(CustomerTaxProfileType.B2BUnregistered, null, vatExemption, defaultSalesVatCategoryId);

    public static CustomerTaxProfile B2CConsumer(bool vatExemption, Guid? defaultSalesVatCategoryId) =>
        new(CustomerTaxProfileType.B2CConsumer, null, vatExemption, defaultSalesVatCategoryId);
}

public enum CustomerTaxProfileType
{
    B2BRegistered,
    B2BUnregistered,
    B2CConsumer,
}
