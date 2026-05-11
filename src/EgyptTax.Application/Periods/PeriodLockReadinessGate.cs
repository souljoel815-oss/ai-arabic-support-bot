using EgyptTax.Application.Compliance;

namespace EgyptTax.Application.Periods;

/// <summary>
/// P3.6 / P2.5 — pure decision logic that turns a
/// <see cref="MonthlyTaxClosingCockpit"/> snapshot into "is this
/// period safe to lock?". Sits between the cockpit query and the
/// <c>LockTaxPeriodHandler</c> so both the UI ("disable the Lock
/// button + show why") and the handler ("refuse the command unless
/// gated past") stay in lockstep on the same rules.
///
/// Two tiers of blocker:
///
///   • <b>Hard blockers</b> — drafts dated in the period, failed
///     ETA submissions in the period. These are bookkeeping
///     mistakes the operator MUST resolve; locking on top of them
///     leaves the period in an inconsistent state (drafts will roll
///     forward; failed submissions will silently disappear from the
///     dashboard's "in this month" filter once locked). The force-
///     lock escape hatch does NOT bypass these.
///
///   • <b>Soft blockers</b> — missing-document buckets (deductible
///     posts without attachments, posted invoices without ETA rows).
///     These warrant a fix but are sometimes legitimately deferred
///     (e.g., a paper attachment is in the post and the operator
///     wants to file the return on time). An Administrator can
///     force-lock past these by supplying a non-empty
///     <c>ForceLockReason</c>; the audit trail records both the
///     reason and the bucket counts at lock time.
///
/// The gate is a static utility — no state, no DI, no I/O.
/// </summary>
public static class PeriodLockReadinessGate
{
    public static PeriodLockReadinessDecision Evaluate(
        MonthlyTaxClosingCockpit cockpit,
        bool forceRequested)
    {
        ArgumentNullException.ThrowIfNull(cockpit);

        var hardBlockers = new List<PeriodLockBlocker>();
        if (cockpit.DraftsInPeriodCount > 0)
        {
            hardBlockers.Add(new PeriodLockBlocker(
                Code: "DRAFTS_IN_PERIOD",
                DescriptionEn: $"{cockpit.DraftsInPeriodCount} draft document(s) dated inside this period must be posted or voided.",
                DescriptionAr: $"{cockpit.DraftsInPeriodCount} مستند مسودة بتواريخ داخل هذه الفترة لازم يترحّل أو يتلغي.",
                Count: cockpit.DraftsInPeriodCount));
        }
        if (cockpit.FailedEtaSubmissionCount > 0)
        {
            hardBlockers.Add(new PeriodLockBlocker(
                Code: "FAILED_ETA_SUBMISSIONS",
                DescriptionEn: $"{cockpit.FailedEtaSubmissionCount} ETA submission(s) in this period are in the Failed state. Resolve them on the ETA dashboard before locking.",
                DescriptionAr: $"{cockpit.FailedEtaSubmissionCount} إرسال إلى مصلحة الضرائب الإلكترونية فاشل في هذه الفترة. اتعامل معاهم من لوحة ETA قبل القفل.",
                Count: cockpit.FailedEtaSubmissionCount));
        }

        var softBlockers = new List<PeriodLockBlocker>();
        foreach (var bucket in cockpit.MissingDocuments)
        {
            if (bucket.Count == 0) continue;
            softBlockers.Add(new PeriodLockBlocker(
                Code: $"MISSING.{bucket.Name.ToUpperInvariant().Replace(' ', '_')}",
                DescriptionEn: $"{bucket.Count} document(s) in bucket \"{bucket.Name}\" still missing.",
                DescriptionAr: $"{bucket.Count} مستند في مجموعة \"{bucket.Name}\" لسه ناقصة.",
                Count: bucket.Count));
        }

        // Period-lock checklist items the cockpit query computed but
        // that we don't already cover under hard/soft. Anything still
        // ✗ on the checklist that wasn't surfaced as a more-specific
        // blocker collapses to a soft blocker so the gate doesn't
        // give a false green.
        foreach (var item in cockpit.PeriodLockChecklist)
        {
            if (item.Cleared) continue;
            // Skip items already represented by hard/soft above to
            // avoid a duplicate "drafts in period" line.
            if (item.Description.Contains("draft", StringComparison.OrdinalIgnoreCase)) continue;
            if (item.Description.Contains("ETA", StringComparison.Ordinal)) continue;
            if (item.Description.Contains("missing", StringComparison.OrdinalIgnoreCase)) continue;
            softBlockers.Add(new PeriodLockBlocker(
                Code: "CHECKLIST_OPEN",
                DescriptionEn: item.Description,
                DescriptionAr: item.Description,
                Count: item.RelatedCount ?? 0));
        }

        var allowed = hardBlockers.Count == 0 && (softBlockers.Count == 0 || forceRequested);
        var requiresForce = hardBlockers.Count == 0 && softBlockers.Count > 0;

        return new PeriodLockReadinessDecision(
            Allowed: allowed,
            RequiresForce: requiresForce,
            HardBlockers: hardBlockers,
            SoftBlockers: softBlockers);
    }
}

public sealed record PeriodLockReadinessDecision(
    bool Allowed,
    bool RequiresForce,
    IReadOnlyList<PeriodLockBlocker> HardBlockers,
    IReadOnlyList<PeriodLockBlocker> SoftBlockers);

public sealed record PeriodLockBlocker(
    string Code,
    string DescriptionEn,
    string DescriptionAr,
    int Count);
