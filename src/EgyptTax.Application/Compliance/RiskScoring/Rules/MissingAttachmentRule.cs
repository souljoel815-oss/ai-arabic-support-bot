using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T141 / FR-016 — fires when at least one purchase-invoice line is
/// marked deductible but the document has zero attachments. The
/// <see cref="EgyptTax.Infrastructure.Purchases.PostPurchaseInvoiceHandler"/>
/// rejects this at post-time as an invariant violation; the rule
/// surfaces it earlier so the operator sees the gap before clicking
/// Post. Severity: <see cref="RiskSeverity.Blocker"/> — the post
/// will be blocked, so the badge needs to communicate that.
/// </summary>
public sealed class MissingAttachmentRule : IPurchaseDocumentRiskRule
{
    public string RuleId => "PURCHASE_INVOICE.MISSING_ATTACHMENT";

    public IReadOnlyList<RiskFinding> Evaluate(PurchaseDocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deductibleLineCount = context.Invoice.Lines.Count(l => l.DeductibleFlag);
        if (deductibleLineCount == 0 || context.Attachments.Count > 0)
        {
            return Array.Empty<RiskFinding>();
        }

        return
        [
            new RiskFinding(
                RuleId,
                RiskSeverity.Blocker,
                new ArabicEnglishText(
                    "مرفقات مفقودة لخصم ضريبي",
                    "Missing attachment for deductible expense"
                ),
                new ArabicEnglishText(
                    $"تحتوي الفاتورة على {deductibleLineCount} سطر(أسطر) قابل(ة) للخصم ولكن لا توجد مرفقات. لن يقبل النظام النشر دون مستند داعم (FR-016).",
                    $"Invoice has {deductibleLineCount} deductible line(s) but no attachments. Per FR-016 the post will be blocked until at least one supporting document is uploaded."
                ),
                FixHint: "Upload the supplier's PDF / scanned receipt before posting."
            ),
        ];
    }
}
