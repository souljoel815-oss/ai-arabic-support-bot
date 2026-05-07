namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// US2 — fan-out scorer for <see cref="IPurchaseDocumentRiskRule"/>s.
/// Mirrors <see cref="DocumentRiskScorer"/> on the buy side:
/// preserves emission order (Severity desc → RuleId asc) so the
/// badge picks the worst finding off index 0.
/// </summary>
public sealed class PurchaseDocumentRiskScorer
{
    private readonly IReadOnlyList<IPurchaseDocumentRiskRule> _rules;

    public PurchaseDocumentRiskScorer(IEnumerable<IPurchaseDocumentRiskRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    public IReadOnlyList<RiskFinding> Score(PurchaseDocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = _rules.SelectMany(r => r.Evaluate(context)).ToList();
        findings.Sort((a, b) =>
        {
            var s = b.Severity.CompareTo(a.Severity);
            return s != 0 ? s : string.CompareOrdinal(a.RuleId, b.RuleId);
        });
        return findings;
    }
}
