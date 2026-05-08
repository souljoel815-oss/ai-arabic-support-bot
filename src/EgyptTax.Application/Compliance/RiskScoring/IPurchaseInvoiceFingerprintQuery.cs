namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// T142 — port the caller invokes before scoring a PurchaseInvoice
/// to load the candidate-duplicate set. Pulled into the
/// <see cref="PurchaseDocumentRiskContext"/> so the
/// <see cref="Rules.DuplicateSupplierInvoiceRule"/> stays infra-free.
///
/// The query is keyed by (supplier + supplier-invoice-number) — the
/// covering index `ix_purchase_invoices_supplier_dedup` keeps the
/// lookup seek-friendly even on large datasets. Excludes the
/// subject document itself so a Draft-state invoice doesn't flag
/// itself.
/// </summary>
public interface IPurchaseInvoiceFingerprintQuery
{
    Task<
        IReadOnlyList<PurchaseInvoiceFingerprint>
    > FindOtherPurchaseInvoicesWithSameSupplierReferenceAsync(
        Guid supplierId,
        string supplierInvoiceNumber,
        Guid? excludePurchaseInvoiceId,
        CancellationToken cancellationToken = default
    );
}
