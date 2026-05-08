using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B3a — embedded tax profile per FR-041. Snapshotted onto the
/// PurchaseInvoice header at post-time so master-data corrections
/// later don't silently change the historical recoverability of
/// input VAT. Factory methods enforce the data-model invariant
/// <c>tin non-null iff profile_type = RegisteredTaxpayer</c> and
/// <c>reverse_charge_flag true only when ForeignSupplier</c>.
/// Persistence stores the TIN as a raw 9-digit string for clean
/// EF mapping; <see cref="Tin"/> re-parses through
/// <see cref="EgyptianTin"/> for typed access.
/// </summary>
public readonly record struct SupplierTaxProfile(
    SupplierTaxProfileType ProfileType,
    string? TinValue,
    bool ReverseChargeFlag,
    Guid? DefaultPurchaseVatCategoryId
)
{
    public EgyptianTin? Tin => string.IsNullOrEmpty(TinValue) ? null : EgyptianTin.Parse(TinValue);

    /// <summary>
    /// FR-020 / FR-041 — input VAT is recoverable only when the
    /// supplier is a registered taxpayer. The Tax Risk Score rule
    /// `NonRecoverableInputVatRule` (T143) flags purchase-invoice
    /// rows that mark deductible-input-VAT against a non-registered
    /// supplier; the rule's own decision uses this property.
    /// </summary>
    public bool InputVatRecoverable => ProfileType == SupplierTaxProfileType.RegisteredTaxpayer;

    public static SupplierTaxProfile RegisteredTaxpayer(
        EgyptianTin tin,
        Guid? defaultPurchaseVatCategoryId
    ) =>
        new(
            SupplierTaxProfileType.RegisteredTaxpayer,
            tin.Value,
            false,
            defaultPurchaseVatCategoryId
        );

    public static SupplierTaxProfile Unregistered(Guid? defaultPurchaseVatCategoryId) =>
        new(SupplierTaxProfileType.Unregistered, null, false, defaultPurchaseVatCategoryId);

    public static SupplierTaxProfile ForeignSupplier(Guid? defaultPurchaseVatCategoryId) =>
        new(
            SupplierTaxProfileType.ForeignSupplier,
            null,
            ReverseChargeFlag: true,
            defaultPurchaseVatCategoryId
        );
}

public enum SupplierTaxProfileType
{
    RegisteredTaxpayer,
    Unregistered,
    ForeignSupplier,
}
