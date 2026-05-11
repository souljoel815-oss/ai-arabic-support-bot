namespace EgyptTax.Application.Periods;

/// <summary>
/// P3.6 / P2.5 — thrown by <c>LockTaxPeriodHandler</c> when the
/// period-lock gate refuses the command. Carries the gate decision
/// so callers (page UI, API) can render every individual blocker
/// instead of "lock failed".
/// </summary>
public sealed class PeriodLockBlockedException : InvalidOperationException
{
    public PeriodLockReadinessDecision Decision { get; }

    public PeriodLockBlockedException(PeriodLockReadinessDecision decision)
        : base(BuildMessage(decision))
    {
        Decision = decision;
    }

    private static string BuildMessage(PeriodLockReadinessDecision decision)
    {
        if (decision.HardBlockers.Count > 0)
        {
            return "Cannot lock period — "
                + string.Join("; ", decision.HardBlockers.Select(b => b.DescriptionEn));
        }
        return "Cannot lock period — soft blockers present and no force-lock reason supplied: "
            + string.Join("; ", decision.SoftBlockers.Select(b => b.DescriptionEn));
    }
}
