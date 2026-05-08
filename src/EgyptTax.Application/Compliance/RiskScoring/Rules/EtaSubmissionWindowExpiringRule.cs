using EgyptTax.Domain.Eta;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring.Rules;

/// <summary>
/// T114 — surfaces an Active-ETA-Compliance-Dashboard signal directly
/// on the document badge: if the open ETA submission row is still
/// non-final (Pending or Failed) AND its 7-day window expires within
/// 24 hours, show a Warning; if within 6 hours,
/// <see cref="RiskSeverity.MustFixBeforeFiling"/>; if already expired,
/// <see cref="RiskSeverity.Blocker"/>. Submitted rows are silent — the
/// regulator already accepted the document.
/// </summary>
public sealed class EtaSubmissionWindowExpiringRule : IDocumentRiskRule
{
    public string RuleId => "ETA.SUBMISSION_WINDOW_EXPIRING";

    public IReadOnlyList<RiskFinding> Evaluate(DocumentRiskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sub = context.EtaSubmission;
        if (sub is null || sub.Status == EtaSubmissionStatus.Submitted)
        {
            return Array.Empty<RiskFinding>();
        }

        var remaining = sub.SubmissionWindowExpiresAtUtc - context.NowUtc;

        if (remaining <= TimeSpan.Zero)
        {
            return
            [
                new RiskFinding(
                    RuleId,
                    RiskSeverity.Blocker,
                    new ArabicEnglishText(
                        "انتهت نافذة الإرسال",
                        "ETA submission window has expired"
                    ),
                    new ArabicEnglishText(
                        $"انقضت نافذة الإرسال البالغة 7 أيام في {sub.SubmissionWindowExpiresAtUtc:yyyy-MM-dd HH:mm} UTC. لم يعد بإمكان النظام تقديم هذه الفاتورة تلقائياً وقد تتطلب تدخلاً يدوياً من المشغل.",
                        $"The 7-day window closed at {sub.SubmissionWindowExpiresAtUtc:yyyy-MM-dd HH:mm} UTC. Automatic submission is no longer possible; manual intervention is required."
                    ),
                    FixHint: "Contact ETA for a manual override or document the missed window."
                ),
            ];
        }

        if (remaining <= TimeSpan.FromHours(6))
        {
            return
            [
                new RiskFinding(
                    RuleId,
                    RiskSeverity.MustFixBeforeFiling,
                    new ArabicEnglishText(
                        "نافذة الإرسال على وشك الانتهاء",
                        "ETA submission window closes very soon"
                    ),
                    new ArabicEnglishText(
                        $"تنتهي نافذة الإرسال خلال {(int)remaining.TotalHours} ساعة. يجب التحقق من سبب فشل المحاولات السابقة وإعادة الإرسال.",
                        $"Window closes in {(int)remaining.TotalHours} hour(s). Investigate prior failure attempts and re-submit immediately."
                    ),
                    FixHint: "Open the ETA dashboard and trigger a manual retry."
                ),
            ];
        }

        if (remaining <= TimeSpan.FromHours(24))
        {
            return
            [
                new RiskFinding(
                    RuleId,
                    RiskSeverity.Warning,
                    new ArabicEnglishText(
                        "نافذة الإرسال خلال 24 ساعة",
                        "ETA submission window closes within 24h"
                    ),
                    new ArabicEnglishText(
                        $"تنتهي نافذة الإرسال البالغة 7 أيام في {sub.SubmissionWindowExpiresAtUtc:yyyy-MM-dd HH:mm} UTC.",
                        $"7-day window closes at {sub.SubmissionWindowExpiresAtUtc:yyyy-MM-dd HH:mm} UTC."
                    ),
                    FixHint: "Verify the document is queued for the next retry tick."
                ),
            ];
        }

        return Array.Empty<RiskFinding>();
    }
}
