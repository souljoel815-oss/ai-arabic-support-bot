namespace EgyptTax.Application.Compliance.PenaltyShield;

/// <summary>
/// Snapshot of an issuer's current penalty exposure, computed by
/// <c>IPenaltyExposureCalculator</c>. The dashboard renders these
/// numbers as KPIs (with tier-coloured tones) + an action queue.
/// </summary>
public sealed record PenaltyExposureReport(
    DateTime AsOfUtc,
    PenaltyTier CurrentTier,
    int LateInRollingWindow,
    int LateThisMonth,
    decimal ProjectedThisMonthEgp,
    decimal AvoidableIfActedTodayEgp,
    int CriticalNext24h,
    int CriticalNext7d,
    IReadOnlyList<PenaltyActionItem> WorkQueue);

/// <summary>
/// One row in the prioritised work queue. The user clicks
/// <see cref="SalesInvoiceId"/> to open the document and resubmit.
/// </summary>
public sealed record PenaltyActionItem(
    Guid SalesInvoiceId,
    string? DocumentNumber,
    decimal GrandTotalEgp,
    DateTime DeadlineUtc,
    double HoursUntilDeadline,
    PenaltyActionUrgency Urgency,
    decimal AvoidableFineEgp);

public enum PenaltyActionUrgency
{
    /// <summary>Already past the regulator's window — fine has crystallised.</summary>
    Overdue,
    /// <summary>Less than 24h to the deadline. Submit now.</summary>
    Critical,
    /// <summary>Within 7 days of deadline. Plan to submit.</summary>
    Soon,
}
