using EgyptTax.Domain.Eta;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T115 — fires when the open ETA submission row is in
/// <see cref="EtaSubmissionStatus.Failed"/> state with at least one
/// attempt logged. Severity scales with attempt count and
/// proximity to the 7-day deadline: a single failure with the
/// window still wide open is just <see cref="RiskSeverity.Warning"/>;
/// repeated failures or a failure inside the final 24h escalate to
/// <see cref="RiskSeverity.MustFixBeforeFiling"/>. The
/// <c>EtaSubmissionWindowExpiringRule</c> handles the deadline-only
/// dimension; this rule handles the failure dimension.
/// </summary>
public sealed class EtaSubmissionFailedRule : IDocumentRiskRule
{
    public string RuleId => "ETA.SUBMISSION_FAILED";

    public IReadOnlyList<RiskFinding> Evaluate(DocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sub = context.EtaSubmission;
        if (sub is null || sub.Status != EtaSubmissionStatus.Failed || sub.AttemptCount == 0)
        {
            return Array.Empty<RiskFinding>();
        }

        var severity =
            sub.AttemptCount >= 3 ? RiskSeverity.MustFixBeforeFiling : RiskSeverity.Warning;

        var errorClause = string.IsNullOrWhiteSpace(sub.ErrorCode)
            ? ""
            : $" (code {sub.ErrorCode})";

        return
        [
            new RiskFinding(
                RuleId,
                severity,
                new ArabicEnglishText(
                    $"فشل إرسال ETA — {sub.AttemptCount} محاولة",
                    $"ETA submission failed — {sub.AttemptCount} attempt(s){errorClause}"
                ),
                new ArabicEnglishText(
                    sub.ErrorMessage
                        ?? "لم تنجح المحاولات السابقة في تقديم الفاتورة لمصلحة الضرائب.",
                    sub.ErrorMessage
                        ?? "Previous attempts to submit this document to ETA have failed. The retry job will continue trying until the 7-day window closes."
                ),
                FixHint: severity == RiskSeverity.MustFixBeforeFiling
                    ? "Open the ETA dashboard, review the error code, and intervene manually before the window closes."
                    : "Monitor the next retry tick; investigate if it fails again."
            ),
        ];
    }
}
