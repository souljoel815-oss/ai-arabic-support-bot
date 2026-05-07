namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// US2 — buy-side counterpart of <see cref="IDocumentRiskRule"/>.
/// Same plain-C# pattern; rules are stateless DI singletons; new
/// rules ship by adding a class plus one DI registration.
/// </summary>
public interface IPurchaseDocumentRiskRule
{
    string RuleId { get; }
    IReadOnlyList<RiskFinding> Evaluate(PurchaseDocumentRiskContext context);
}
