using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T143 / FR-020 / FR-041 / INV-011 — fires when a purchase invoice
/// carries lines marked deductible against a supplier whose tax
/// profile is NOT <see cref="SupplierTaxProfileType.RegisteredTaxpayer"/>.
/// Per FR-020, input VAT is recoverable only against registered
/// taxpayers; deducting against an Unregistered or ForeignSupplier
/// (where reverse-charge applies and the buyer is the one
/// self-invoicing the output VAT) is a frequent mistake that
/// produces over-claimed input-VAT positions.
///
/// The rule reads the SNAPSHOT on the invoice rather than the live
/// supplier profile so historical decisions remain stable when the
/// supplier later registers — the audit trail wants the answer that
/// applied at post-time.
///
/// Severity: <see cref="RiskSeverity.MustFixBeforeFiling"/> rather
/// than Blocker because the operator may legitimately mark
/// deductible despite the supplier profile (e.g., they're about to
/// update the supplier's profile to Registered after receiving the
/// missing TIN); the rule alerts them, not stops them.
/// </summary>
public sealed class NonRecoverableInputVatRule : IPurchaseDocumentRiskRule
{
    public string RuleId => "PURCHASE_INVOICE.NON_RECOVERABLE_INPUT_VAT";

    public IReadOnlyList<RiskFinding> Evaluate(PurchaseDocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var snapshot = context.Invoice.SupplierTaxProfileSnapshot;
        if (snapshot.InputVatRecoverable)
        {
            return Array.Empty<RiskFinding>();
        }

        var deductibleLines = context.Invoice.Lines.Count(l => l.DeductibleFlag);
        if (deductibleLines == 0)
        {
            return Array.Empty<RiskFinding>();
        }

        var profileLabel = snapshot.ProfileType switch
        {
            SupplierTaxProfileType.Unregistered => "Unregistered",
            SupplierTaxProfileType.ForeignSupplier => "Foreign supplier (reverse-charge)",
            _ => snapshot.ProfileType.ToString(),
        };

        return [new RiskFinding(
            RuleId,
            RiskSeverity.MustFixBeforeFiling,
            new ArabicEnglishText(
                "ضريبة المدخلات غير قابلة للاسترداد",
                $"Input VAT not recoverable — supplier is {profileLabel}"),
            new ArabicEnglishText(
                $"تم وضع علامة على {deductibleLines} سطر(أسطر) كقابل للخصم رغم أن المورد ليس مسجلاً كدافع ضريبة. لن تقبل مصلحة الضرائب استرداد ضريبة المدخلات.",
                $"{deductibleLines} line(s) are marked deductible but the supplier's tax profile is {profileLabel} (input-VAT recoverable only against RegisteredTaxpayer per FR-020). The claim will be disallowed at filing."),
            FixHint: snapshot.ProfileType == SupplierTaxProfileType.ForeignSupplier
                ? "Use the reverse-charge surface instead of the deductible-input-VAT surface."
                : "Either obtain the supplier's TIN and update their profile to RegisteredTaxpayer, or unmark the deductible flag.")];
    }
}
