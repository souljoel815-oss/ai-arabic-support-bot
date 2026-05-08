using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T212 / FR-045 / Differentiator 1 / US7 — surfaces an Info-level
/// hint when a deductible purchase from a registered taxpayer
/// supplier is recorded: WHT MAY apply when the operator pays this
/// invoice. The rule fires at INVOICE time (not payment time)
/// because the badge needs to show on the document the operator is
/// looking at; the hint reminds them to verify the WHT-category
/// configuration before posting the supplier-payment voucher.
///
/// Severity is intentionally <see cref="RiskSeverity.Info"/> — the
/// rule can't tell whether THIS specific supplier is genuinely
/// services-based without a per-supplier WHT-applicable flag (a
/// future enhancement). The hint is a reminder, not an accusation;
/// the operator is expected to dismiss when not relevant.
///
/// Triggers: deductible line(s) on the invoice + supplier tax
/// profile is RegisteredTaxpayer (the WHT-attractive case per
/// FR-045). Foreign + Unregistered suppliers don't attract WHT
/// the same way and skip the hint.
/// </summary>
public sealed class WhtRequiredButMissingRule : IPurchaseDocumentRiskRule
{
    public string RuleId => "PURCHASE_INVOICE.WHT_REQUIRED_BUT_MISSING";

    public IReadOnlyList<RiskFinding> Evaluate(PurchaseDocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only deductible purchases from registered taxpayers.
        var hasDeductible = context.Invoice.Lines.Any(l => l.DeductibleFlag);
        if (!hasDeductible)
            return Array.Empty<RiskFinding>();

        var supplierProfile = context.Invoice.SupplierTaxProfileSnapshot;
        if (supplierProfile.ProfileType != SupplierTaxProfileType.RegisteredTaxpayer)
        {
            return Array.Empty<RiskFinding>();
        }

        return
        [
            new RiskFinding(
                RuleId,
                RiskSeverity.Info,
                new ArabicEnglishText("قد تستحق ضريبة الخصم", "Withholding tax may apply"),
                new ArabicEnglishText(
                    "هذه الفاتورة من مورد مسجل وتحتوي على بنود قابلة للخصم — تحقق من فئة الخصم المعتمدة قبل سداد المورد (FR-045).",
                    "This is a deductible purchase from a registered taxpayer; per FR-045, WHT may apply when you settle the payment. Verify the WHT-category configuration is in place before posting the supplier-payment voucher."
                ),
                FixHint: "Configure or confirm a WHT category in Settings → WHT Categories effective on the payment date, then apply it on the supplier-payment voucher."
            ),
        ];
    }
}
