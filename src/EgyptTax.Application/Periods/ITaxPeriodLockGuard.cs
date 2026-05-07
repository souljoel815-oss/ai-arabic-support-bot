using EgyptTax.Domain.Periods;

namespace EgyptTax.Application.Periods;

/// <summary>
/// FR-037 / SC-004 — port the post handlers call before allocating
/// a document number to verify the document's date does NOT fall
/// inside a Locked tax period. Returns whether posting is allowed
/// AND (when blocked) the lock metadata so the caller can render a
/// useful message instead of a generic rejection.
/// </summary>
public interface ITaxPeriodLockGuard
{
    Task<TaxPeriodLockCheck> CheckVatMonthAsync(
        DateOnly documentDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a lock-guard check. <see cref="IsLocked"/>=true means
/// the post must be rejected; the metadata fields describe WHEN
/// and BY WHOM the lock was placed.
/// </summary>
public sealed record TaxPeriodLockCheck(
    bool IsLocked,
    int Year,
    int MonthOrQuarter,
    DateTime? LockedAtUtc,
    Guid? LockedByUserId,
    string? LockedReason);
