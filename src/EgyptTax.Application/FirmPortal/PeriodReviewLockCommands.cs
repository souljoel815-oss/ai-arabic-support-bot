namespace EgyptTax.Application.FirmPortal;

/// <summary>
/// US8 / FR-050 — bookkeeper signals "I'm done editing this month,
/// please review" by locking. The hard period close (TaxPeriod.Lock)
/// happens later after the VAT return is filed; this is the soft
/// pre-close handshake. At most one active lock per (year, month);
/// re-locking after release works via a fresh row.
/// </summary>
public sealed record LockForReviewCommand(
    int PeriodYear,
    int PeriodMonth,
    Guid LockedByUserId,
    string? Note = null);

/// <summary>US8 / FR-050 — release the soft lock when review is done.</summary>
public sealed record ReleaseReviewCommand(
    Guid PeriodReviewLockId,
    Guid ReleasedByUserId);
