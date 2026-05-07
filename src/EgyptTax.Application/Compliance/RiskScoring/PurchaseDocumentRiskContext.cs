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
/// (T141 MissingAttachmentRule), an optional duplicate-fingerprint
/// probe (T142, deferred to next batch), and a wall clock.
/// </summary>
public sealed record PurchaseDocumentRiskContext(
    PurchaseInvoice Invoice,
    Supplier? Supplier,
    IReadOnlyList<Attachment> Attachments,
    DateTime NowUtc);
