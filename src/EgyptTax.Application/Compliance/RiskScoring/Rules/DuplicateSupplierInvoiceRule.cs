using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T142 / FR-016 / Differentiator 1 — fires when another posted (or
/// drafted) PurchaseInvoice exists with the same supplier and the
/// same supplier-invoice-number. Caller pre-loads candidates via
/// <see cref="IPurchaseInvoiceFingerprintQuery"/> and supplies them
/// in <see cref="PurchaseDocumentRiskContext.SupplierInvoiceFingerprints"/>.
///
/// Severity ladder:
///  * <see cref="RiskSeverity.Blocker"/> — supplier + invoice number
///    + date + grand-total ALL match. This is almost certainly the
///    same physical document keyed in twice; double-claiming input
///    VAT against it is the classic deductible-fraud signal.
///  * <see cref="RiskSeverity.MustFixBeforeFiling"/> — supplier +
///    invoice number match but date or amount differs. Could be a
///    legitimate re-issue / correction, but operator MUST verify
///    before posting (otherwise input VAT will be claimed twice on
///    the same supplier reference number, and ETA will reject).
///
/// The rule never returns more than one finding even when multiple
/// candidate matches exist; the first match is enough to surface the
/// problem.
/// </summary>
public sealed class DuplicateSupplierInvoiceRule : IPurchaseDocumentRiskRule
{
    public string RuleId => "PURCHASE_INVOICE.DUPLICATE_SUPPLIER_INVOICE";

    public IReadOnlyList<RiskFinding> Evaluate(PurchaseDocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subject = context.Invoice;
        var matches = context.SupplierInvoiceFingerprints
            .Where(f => f.Id != subject.Id) // ignore self
            .Where(f => string.Equals(
                f.SupplierInvoiceNumber.Trim(),
                subject.SupplierInvoiceNumber.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            return Array.Empty<RiskFinding>();
        }

        // Pick the strongest match. Exact (date + amount) → Blocker.
        // Otherwise → MustFixBeforeFiling.
        var exact = matches.FirstOrDefault(f =>
            f.DateReceived == subject.DateReceived
            && f.GrandTotal.Amount == subject.GrandTotal.Amount);

        if (exact is not null)
        {
            return [new RiskFinding(
                RuleId,
                RiskSeverity.Blocker,
                new ArabicEnglishText(
                    "فاتورة مورد مكررة",
                    $"Duplicate supplier invoice — same number + date + amount as {exact.DocumentNumber ?? "(another draft)"}"),
                new ArabicEnglishText(
                    $"المورد ورقم الفاتورة \"{subject.SupplierInvoiceNumber}\" والتاريخ والإجمالي مطابقون لمستند موجود بالفعل ({exact.DocumentNumber ?? "draft"}). إدخال نفس الفاتورة مرتين يؤدي إلى المطالبة بضريبة المدخلات مرتين.",
                    $"Supplier reference number \"{subject.SupplierInvoiceNumber}\" + date {subject.DateReceived:yyyy-MM-dd} + grand total {subject.GrandTotal.Amount:F2} EGP all match an existing document ({exact.DocumentNumber ?? "draft"}). Posting will double-claim input VAT against the same supplier invoice."),
                FixHint: "Verify this is not a duplicate. If it is, discard this draft. If it is a legitimate re-issue, ask the supplier for a new reference number.")];
        }

        var first = matches[0];
        return [new RiskFinding(
            RuleId,
            RiskSeverity.MustFixBeforeFiling,
            new ArabicEnglishText(
                "رقم فاتورة المورد قيد الاستخدام",
                $"Supplier invoice number already used — {first.DocumentNumber ?? "(another draft)"}"),
            new ArabicEnglishText(
                $"تم استخدام رقم الفاتورة \"{subject.SupplierInvoiceNumber}\" مع نفس المورد من قبل (التاريخ أو المبلغ مختلف). تحقق من أن هذا ليس إعادة إصدار للفاتورة نفسها قبل النشر.",
                $"Supplier reference number \"{subject.SupplierInvoiceNumber}\" was used on {matches.Count} prior document(s) with this supplier (date or amount differs). Verify this is a legitimate re-issue / correction before posting."),
            FixHint: "Open the prior document; if it's the same physical invoice with a typo fix, void this draft.")];
    }
}
