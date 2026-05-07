namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// R-18 — plain-C# rule contract. Implementations are registered via
/// DI as singletons (rules are stateless) and the
/// <see cref="DocumentRiskScorer"/> aggregates findings from every
/// registered rule on every score call. New rules ship by adding a
/// class and registering it; no DSL.
/// </summary>
public interface IDocumentRiskRule
{
    /// <summary>
    /// Stable identifier surfaced on the badge tooltip + cockpit
    /// trend report. Format <c>FAMILY.NAME</c> (e.g.
    /// <c>SALES_INVOICE.MISSING_TIN</c>) so families can be grouped
    /// without parsing the human title.
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Evaluate the rule against the supplied context and return zero
    /// or more findings. A rule that does not apply to the supplied
    /// document type returns an empty list — the scorer never branches
    /// on rule type.
    /// </summary>
    IReadOnlyList<RiskFinding> Evaluate(DocumentRiskContext context);
}
