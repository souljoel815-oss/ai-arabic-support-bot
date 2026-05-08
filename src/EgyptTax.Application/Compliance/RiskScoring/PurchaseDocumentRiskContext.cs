using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;

namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// US2 — buy-side counterpart of <see cref="DocumentRiskContext"/>.
/// Built fresh per scoring call. Carries the just-posted (or
/// just-loaded) invoice plus the side data the purchase rules need:
/// the loaded supplier (for `Status` + the live tax-profile when the
/// rule prefers it over the snapshot), the attachment set
/// (T141 MissingAttachmentRule), the fingerprint set of other
/// purchase invoices that share the same (supplier, supplier
/// invoice number) — pre-loaded by the caller via
/// <see cref="IPurchaseInvoiceFingerprintQuery"/> — for T142, and
/// a wall clock.
/// </summary>
public sealed record PurchaseDocumentRiskContext(
    PurchaseInvoice Invoice,
    Supplier? Supplier,
    IReadOnlyList<Attachment> Attachments,
    IReadOnlyList<PurchaseInvoiceFingerprint> SupplierInvoiceFingerprints,
    DateTime NowUtc
);
