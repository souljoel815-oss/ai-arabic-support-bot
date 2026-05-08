using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T112 — fires when a sales invoice (or credit note) is issued to a
/// customer whose tax-profile snapshot is <c>B2BRegistered</c> but
/// whose TIN is empty (a data-integrity bug — the factory should have
/// rejected this — but we still emit a Blocker so it surfaces in
/// case master-data was edited via raw SQL). Also fires at
/// <see cref="RiskSeverity.MustFixBeforeFiling"/> when the snapshot
/// is <c>B2BUnregistered</c>: a B2B transaction without a registered
/// TIN cannot land cleanly on ETA's e-invoice schema and is a
/// frequent source of rejected submissions.
/// </summary>
public sealed class MissingTinRule : IDocumentRiskRule
{
    public string RuleId => "SALES_INVOICE.MISSING_TIN";

    public IReadOnlyList<RiskFinding> Evaluate(DocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = context.Invoice.CustomerTaxProfileSnapshot;

        if (
            snapshot.ProfileType == CustomerTaxProfileType.B2BRegistered
            && string.IsNullOrWhiteSpace(snapshot.TinValue)
        )
        {
            return
            [
                new RiskFinding(
                    RuleId,
                    RiskSeverity.Blocker,
                    new ArabicEnglishText("الرقم الضريبي مفقود", "Tax registration number missing"),
                    new ArabicEnglishText(
                        "العميل مسجل (B2B) ولكن لا يحمل رقمًا ضريبيًا — يجب تصحيح بيانات العميل قبل الإرسال إلى مصلحة الضرائب.",
                        "Customer is marked B2B-Registered but has no Tax Registration Number. ETA submission will reject this document. Fix the customer master record before posting."
                    ),
                    FixHint: "Open the customer record and supply a 9-digit Egyptian TIN."
                ),
            ];
        }

        if (snapshot.ProfileType == CustomerTaxProfileType.B2BUnregistered)
        {
            return
            [
                new RiskFinding(
                    RuleId,
                    RiskSeverity.MustFixBeforeFiling,
                    new ArabicEnglishText(
                        "معاملة B2B بدون رقم ضريبي",
                        "B2B transaction without TIN"
                    ),
                    new ArabicEnglishText(
                        "العميل من نوع B2B-Unregistered؛ مصلحة الضرائب قد ترفض الفاتورة لأن المعاملة بين شركتين دون رقم ضريبي مسجل.",
                        "Customer profile is B2B-Unregistered. ETA may reject the document because a registered TIN is expected on B2B traffic. Confirm the customer is genuinely unregistered."
                    ),
                    FixHint: "If the customer recently registered with ETA, update their profile to B2B-Registered and supply the TIN."
                ),
            ];
        }

        return Array.Empty<RiskFinding>();
    }
}
