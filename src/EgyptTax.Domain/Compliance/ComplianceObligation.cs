namespace EgyptTax.Domain.Compliance;

/// <summary>
/// P2.6 — one statutory filing the company is on the hook for in a
/// specific period. Materialised by
/// <c>ComplianceObligationGenerator</c> from the company's tax-regime
/// + fiscal-year settings, then ticked off by the operator from
/// <c>/compliance/calendar</c>.
///
/// Composite natural key: (Kind, PeriodYear, PeriodOrdinal). The
/// generator is idempotent by this key — re-running for an already
/// materialised period is a no-op so the daily refresh job is safe.
/// </summary>
public sealed class ComplianceObligation
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public ComplianceObligationKind Kind { get; init; }

    /// <summary>Calendar year the obligation belongs to.</summary>
    public int PeriodYear { get; init; }

    /// <summary>
    /// Period ordinal within the year. Semantics depend on
    /// <see cref="Kind"/>: VAT-monthly = 1..12, VAT-quarterly /
    /// Form 41 = 1..4, annual = 1.
    /// </summary>
    public int PeriodOrdinal { get; init; }

    /// <summary>Inclusive period start.</summary>
    public DateOnly PeriodStart { get; init; }
    /// <summary>Inclusive period end.</summary>
    public DateOnly PeriodEnd { get; init; }

    /// <summary>Statutory filing deadline for this obligation.</summary>
    public DateOnly DueDate { get; init; }

    public ComplianceObligationStatus Status { get; private set; }
        = ComplianceObligationStatus.Upcoming;

    public DateTime? FiledAtUtc { get; private set; }
    public string? FilingReference { get; private set; }
    public Guid? ProofAttachmentId { get; private set; }

    private ComplianceObligation() { }

    public ComplianceObligation(
        ComplianceObligationKind kind,
        int periodYear,
        int periodOrdinal,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate)
    {
        if (periodYear < 2020 || periodYear > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(periodYear), "Period year out of supported range.");
        }
        if (periodOrdinal < 1 || periodOrdinal > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(periodOrdinal), "Period ordinal out of range.");
        }
        if (periodStart > periodEnd)
        {
            throw new ArgumentException("Period start cannot be after period end.", nameof(periodStart));
        }

        Kind = kind;
        PeriodYear = periodYear;
        PeriodOrdinal = periodOrdinal;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        DueDate = dueDate;
    }

    /// <summary>
    /// Operator confirms the filing was made. Idempotent re-mark keeps
    /// the original timestamp + reference (avoids accidental overwrite).
    /// </summary>
    public void MarkFiled(DateTime nowUtc, string? reference = null, Guid? proofAttachmentId = null)
    {
        if (Status == ComplianceObligationStatus.Filed) return;
        Status = ComplianceObligationStatus.Filed;
        FiledAtUtc = nowUtc;
        FilingReference = reference;
        ProofAttachmentId = proofAttachmentId;
    }

    /// <summary>
    /// Operator un-marks (e.g., wrong reference attached). Allowed only
    /// before the obligation crosses its due date — the audit trail
    /// for late corrections lives in the audit log, not as a state
    /// transition.
    /// </summary>
    public void Reopen()
    {
        if (Status != ComplianceObligationStatus.Filed) return;
        Status = ComplianceObligationStatus.Upcoming;
        FiledAtUtc = null;
        FilingReference = null;
        ProofAttachmentId = null;
    }

    /// <summary>
    /// Read-only derived view used by the dashboard to colour-code
    /// rows. Encodes Upcoming → Critical → Overdue when the row is
    /// past its due date and still unfiled.
    /// </summary>
    public ComplianceObligationUrgency UrgencyAsOf(DateOnly today)
    {
        if (Status == ComplianceObligationStatus.Filed)
        {
            return ComplianceObligationUrgency.Filed;
        }
        var daysUntil = DueDate.DayNumber - today.DayNumber;
        return daysUntil switch
        {
            < 0  => ComplianceObligationUrgency.Overdue,
            <= 1 => ComplianceObligationUrgency.DueToday,
            <= 7 => ComplianceObligationUrgency.DueThisWeek,
            <= 30 => ComplianceObligationUrgency.Soon,
            _ => ComplianceObligationUrgency.Later,
        };
    }
}

public enum ComplianceObligationKind
{
    /// <summary>Standard regime — monthly VAT return.</summary>
    VatMonthly,
    /// <summary>Law 6/2025 simplified regime — quarterly VAT return.</summary>
    VatQuarterly,
    /// <summary>Quarterly Form 41 — withholding tax.</summary>
    Form41Quarterly,
    /// <summary>Annual corporate income tax (Standard regime).</summary>
    IncomeTaxAnnual,
    /// <summary>Annual turnover tax declaration (Law 6/2025 regime).</summary>
    TurnoverTaxAnnual,
}

public enum ComplianceObligationStatus
{
    Upcoming,
    Filed,
}

public enum ComplianceObligationUrgency
{
    Overdue,
    DueToday,
    DueThisWeek,
    Soon,
    Later,
    Filed,
}
