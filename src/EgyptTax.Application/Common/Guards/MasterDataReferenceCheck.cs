namespace EgyptTax.Application.Common.Guards;

/// <summary>
/// T140 / FR-007 — outcome of a master-data deletion-eligibility
/// check. The guard reports both whether deletion is allowed AND
/// (when it isn't) the count of referencing documents per type so
/// the UI can render a useful "this supplier has 3 posted purchase
/// invoices, deactivate instead" message instead of an opaque
/// rejection.
///
/// Per FR-007 the MVP design is status-only soft delete on the
/// master-data tables; this guard exists so that any explicit
/// hard-delete code path (admin tools, future bulk ops, data
/// migration scripts) checks references before destroying the row.
/// </summary>
public sealed record MasterDataReferenceCheck(
    bool CanDelete,
    IReadOnlyList<MasterDataReference> References)
{
    public static MasterDataReferenceCheck Allowed { get; } =
        new(CanDelete: true, References: Array.Empty<MasterDataReference>());

    public static MasterDataReferenceCheck Blocked(IReadOnlyList<MasterDataReference> references) =>
        new(CanDelete: false, References: references);
}

/// <summary>
/// One per (referencing-table, count) pair so a Supplier with both
/// posted PurchaseInvoices AND draft ones surfaces both rows.
/// </summary>
public sealed record MasterDataReference(
    string ReferencingDocumentType,
    long ReferenceCount);
