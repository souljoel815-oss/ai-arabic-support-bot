namespace EgyptTax.Domain.Workflow;

/// <summary>
/// Discriminator for the 8 MVP document aggregates per data-model.md
/// "Common base: Document". Used by <c>DocumentTypeApprovalSetting</c>
/// (per-type approval-required flag) and by <c>PostedDocumentImmutability
/// Guard</c> to surface the correct correction-path hint (credit note
/// vs reversal voucher) per FR-012.
/// </summary>
public enum DocumentType
{
    SalesInvoice,
    CreditNote,
    PurchaseInvoice,
    Expense,
    JournalVoucher,
    SupplierPaymentVoucher,
    CustomerReceiptVoucher,
    FixedAsset,
}

public static class DocumentTypeExtensions
{
    /// <summary>
    /// FR-012 distinction: tax-impacting documents are corrected via
    /// credit notes (FR-013); non-tax-impacting documents (manual
    /// journal vouchers, payment vouchers) are corrected via reversal
    /// vouchers that reference the original.
    /// </summary>
    public static bool IsTaxImpacting(this DocumentType type) =>
        type switch
        {
            DocumentType.SalesInvoice => true,
            DocumentType.CreditNote => true,
            DocumentType.PurchaseInvoice => true,
            DocumentType.Expense => true,
            DocumentType.FixedAsset => true,
            DocumentType.JournalVoucher => false,
            DocumentType.SupplierPaymentVoucher => false,
            DocumentType.CustomerReceiptVoucher => false,
            _ => false,
        };
}
