using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T113 — fires for every distinct line item on the document whose
/// <see cref="Domain.MasterData.Item.EtaItemCode"/> is empty. ETA's
/// e-invoice schema requires a GS1/commodity code per line; a
/// document with even one missing code will be rejected at submission.
/// Severity is <see cref="RiskSeverity.MustFixBeforeFiling"/> rather
/// than <see cref="RiskSeverity.Blocker"/> because the operator may
/// post the draft with the intent to fix master data and re-submit
/// before the 7-day window expires.
/// </summary>
public sealed class MissingEtaCodeRule : IDocumentRiskRule
{
    public string RuleId => "SALES_INVOICE.MISSING_ETA_ITEM_CODE";

    public IReadOnlyList<RiskFinding> Evaluate(DocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var missing = context.Invoice.Lines
            .Select(l => l.ItemId)
            .Distinct()
            .Where(id => context.Items.TryGetValue(id, out var item)
                && string.IsNullOrWhiteSpace(item.EtaItemCode))
            .Select(id => context.Items[id])
            .ToList();

        if (missing.Count == 0)
        {
            return Array.Empty<RiskFinding>();
        }

        var codes = string.Join(", ", missing.Select(i => i.Code));
        return [new RiskFinding(
            RuleId,
            RiskSeverity.MustFixBeforeFiling,
            new ArabicEnglishText(
                $"كود ETA مفقود ({missing.Count})",
                $"ETA item code missing on {missing.Count} item(s)"),
            new ArabicEnglishText(
                $"الأصناف التالية ليس لها كود ETA: {codes}. سترفض مصلحة الضرائب الإرسال.",
                $"The following items have no ETA item code and will be rejected by the regulator: {codes}."),
            FixHint: "Open Master data → Items and supply the GS1/commodity code from ETA's master list.")];
    }
}
