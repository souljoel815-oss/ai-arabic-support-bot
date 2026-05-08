using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// Differentiator 1 / R-18 — one piece of evidence emitted by a
/// rule. The cockpit (Differentiator 2) and per-document badge
/// (Differentiator 1) both consume <c>IReadOnlyList&lt;RiskFinding&gt;</c>;
/// the badge picks the worst severity for its colour, the cockpit
/// groups by <see cref="RuleId"/> for trend reporting.
/// </summary>
public sealed record RiskFinding(
    string RuleId,
    RiskSeverity Severity,
    ArabicEnglishText Title,
    ArabicEnglishText Description,
    string? FixHint = null
);

/// <summary>
/// Per R-18: 4-level severity ladder. The bare ladder is sufficient
/// for the MVP — finer grading (per-rule weights into a 0..100 score)
/// is a Round-3 enhancement.
/// </summary>
public enum RiskSeverity
{
    Info = 0,
    Warning = 1,
    MustFixBeforeFiling = 2,
    Blocker = 3,
}
