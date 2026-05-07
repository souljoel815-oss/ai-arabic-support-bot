namespace EgyptTax.Domain.Periods;

/// <summary>
/// E4 / FR-037 — represents a single tax-filing period (VAT month
/// or income fiscal year etc.). Once Locked (typically after the
/// VAT return is filed) no new documents may be posted whose
/// document date falls inside it; an Administrator must Reopen
/// before backdated posts are allowed again. Reopen is audit-logged
/// per FR-037.
///
/// One row per (period_kind, year, month_or_quarter). The MVP
/// post-handler guard checks the VatMonth row only;
/// IncomeFiscalYear + WhtQuarter locks are stored here too but the
/// post-time enforcement for those lands when the income-tax
/// return + WHT filing flows ship.
/// </summary>
public sealed class TaxPeriod
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public TaxPeriodKind PeriodKind { get; init; }
    public int Year { get; init; }
    public int MonthOrQuarter { get; init; }

    public TaxPeriodStatus Status { get; private set; } = TaxPeriodStatus.Open;
    public DateTime? LockedAtUtc { get; private set; }
    public Guid? LockedByUserId { get; private set; }
    public string? LockedReason { get; private set; }
    public DateTime? ReopenedAtUtc { get; private set; }
    public Guid? ReopenedByUserId { get; private set; }
    public string? ReopenedReason { get; private set; }

    private TaxPeriod() { }

    public TaxPeriod(TaxPeriodKind periodKind, int year, int monthOrQuarter)
    {
        if (year is < 1900 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be a 4-digit calendar year.");
        }
        switch (periodKind)
        {
            case TaxPeriodKind.VatMonth when monthOrQuarter is < 1 or > 12:
                throw new ArgumentOutOfRangeException(nameof(monthOrQuarter), "VAT month must be in [1, 12].");
            case TaxPeriodKind.WhtQuarter when monthOrQuarter is < 1 or > 4:
                throw new ArgumentOutOfRangeException(nameof(monthOrQuarter), "WHT quarter must be in [1, 4].");
            case TaxPeriodKind.IncomeFiscalYear when monthOrQuarter != 0:
                throw new ArgumentOutOfRangeException(nameof(monthOrQuarter), "IncomeFiscalYear uses 0 as the month_or_quarter sentinel.");
        }

        PeriodKind = periodKind;
        Year = year;
        MonthOrQuarter = monthOrQuarter;
    }

    /// <summary>
    /// FR-037 — close the period. Idempotent in the sense that
    /// re-locking an already-Locked period is a no-op (the original
    /// lock metadata is preserved); calling Lock on a previously
    /// Reopened period overwrites the prior lock metadata so the
    /// audit trail reflects the most recent close.
    /// </summary>
    public void Lock(Guid lockedByUserId, DateTime lockedAtUtc, string? reason)
    {
        if (Status == TaxPeriodStatus.Locked)
        {
            return; // idempotent
        }
        Status = TaxPeriodStatus.Locked;
        LockedByUserId = lockedByUserId;
        LockedAtUtc = lockedAtUtc;
        LockedReason = reason;
    }

    /// <summary>
    /// FR-037 — Administrator-only reopen. Throws if already Open.
    /// The previous Lock metadata is intentionally preserved so the
    /// audit chain can show "the period was closed at X then reopened
    /// at Y" without a separate history table; only one Lock /
    /// Reopen pair is retained on the entity itself.
    /// </summary>
    public void Reopen(Guid reopenedByUserId, DateTime reopenedAtUtc, string? reason)
    {
        if (Status == TaxPeriodStatus.Open)
        {
            throw new InvalidOperationException(
                $"Tax period {PeriodKind} {Year}-{MonthOrQuarter:D2} is already Open.");
        }
        Status = TaxPeriodStatus.Open;
        ReopenedByUserId = reopenedByUserId;
        ReopenedAtUtc = reopenedAtUtc;
        ReopenedReason = reason;
    }
}

public enum TaxPeriodKind
{
    VatMonth,
    IncomeFiscalYear,
    WhtQuarter,
}

public enum TaxPeriodStatus
{
    Open,
    Locked,
}
