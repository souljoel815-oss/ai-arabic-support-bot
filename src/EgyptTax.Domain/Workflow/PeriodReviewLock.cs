namespace EgyptTax.Domain.Workflow;

/// <summary>
/// E3 / FR-050 / US8 — soft "lock-for-review" handshake distinct
/// from <see cref="EgyptTax.Domain.Periods.TaxPeriod"/>'s hard close.
///
/// The bookkeeper signals "I'm done editing this month, please
/// review" by locking; the accountant (firm user or in-house) reviews
/// + posts adjusting journal vouchers; either party releases the
/// lock when review is over. The hard <see cref="EgyptTax.Domain.Periods.TaxPeriod"/>
/// close still happens later (after VAT return is filed); this lock
/// is the soft pre-close phase.
///
/// While locked:
/// <list type="bullet">
///   <item>Bookkeepers cannot edit primary documents whose document
///   date falls in the locked month (US8 scenario 3).</item>
///   <item>Accountants (in-house OR firm users) CAN post adjusting
///   journal vouchers via FR-031 (US8 scenario 4).</item>
/// </list>
///
/// The aggregated <see cref="AccountantActionsDuringLock"/> counter
/// is incremented each time the accountant posts an adjusting voucher
/// targeting the locked month; surfaced on release as a "review
/// activity report" so the operator can see what changed during the
/// review window.
/// </summary>
public sealed class PeriodReviewLock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }

    public DateTime LockedAtUtc { get; init; }
    public Guid LockedByUserId { get; init; }
    public string? LockedNote { get; private set; }

    public DateTime? ReleasedAtUtc { get; private set; }
    public Guid? ReleasedByUserId { get; private set; }

    public int AccountantActionsDuringLock { get; private set; }

    private PeriodReviewLock() { }

    public PeriodReviewLock(
        int periodYear,
        int periodMonth,
        DateTime lockedAtUtc,
        Guid lockedByUserId,
        string? lockedNote = null
    )
    {
        if (periodYear is < 1900 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(periodYear),
                "Year must be a 4-digit calendar year."
            );
        }
        if (periodMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(periodMonth), "Month must be in [1, 12].");
        }
        if (lockedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "LockedByUserId must be a non-empty Guid.",
                nameof(lockedByUserId)
            );
        }

        PeriodYear = periodYear;
        PeriodMonth = periodMonth;
        LockedAtUtc = lockedAtUtc;
        LockedByUserId = lockedByUserId;
        LockedNote = lockedNote;
    }

    public bool IsActive => ReleasedAtUtc is null;

    /// <summary>
    /// Increment the activity counter — called by the adjusting-voucher
    /// posting handler each time an accountant posts a voucher whose
    /// document date falls inside this lock's month. The counter is
    /// surfaced on release so the operator can see how much review
    /// activity happened.
    /// </summary>
    public void RecordAccountantAction()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException(
                $"Cannot record activity on a released review-lock for {PeriodYear}-{PeriodMonth:D2}."
            );
        }
        AccountantActionsDuringLock++;
    }

    /// <summary>
    /// FR-050 / US8 — release the soft lock. Idempotent on the
    /// happy path: re-releasing an already-released row is a no-op
    /// (the original release timestamp wins). Once released, the
    /// row is read-only history; a fresh lock-for-review starts a
    /// new row.
    /// </summary>
    public void Release(DateTime releasedAtUtc, Guid releasedByUserId)
    {
        if (releasedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "ReleasedByUserId must be a non-empty Guid.",
                nameof(releasedByUserId)
            );
        }
        ReleasedAtUtc ??= releasedAtUtc;
        ReleasedByUserId ??= releasedByUserId;
    }
}
