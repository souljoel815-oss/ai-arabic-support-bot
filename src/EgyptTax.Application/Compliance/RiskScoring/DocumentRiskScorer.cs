namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// Composes the rule set and runs them all. The scorer itself is
/// trivial — fan-out + flatten — so the per-rule unit tests carry
/// the real coverage. The result preserves emission order
/// (Severity desc → RuleId asc) so the UI can render the worst
/// finding first.
/// </summary>
public sealed class DocumentRiskScorer
{
    private readonly IReadOnlyList<IDocumentRiskRule> _rules;

    public DocumentRiskScorer(IEnumerable<IDocumentRiskRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    public IReadOnlyList<RiskFinding> Score(DocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = _rules.SelectMany(r => r.Evaluate(context)).ToList();
        findings.Sort(
            (a, b) =>
            {
                var s = b.Severity.CompareTo(a.Severity);
                return s != 0 ? s : string.CompareOrdinal(a.RuleId, b.RuleId);
            }
        );
        return findings;
    }

    /// <summary>
    /// Convenience accessor: the headline severity used by the badge
    /// component to colour itself. Returns <c>null</c> when there are
    /// no findings.
    /// </summary>
    public static RiskSeverity? HighestSeverity(IReadOnlyList<RiskFinding> findings) =>
        findings.Count == 0 ? null : findings.Max(f => f.Severity);
}
