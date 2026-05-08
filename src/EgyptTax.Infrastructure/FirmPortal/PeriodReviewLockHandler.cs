using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.FirmPortal;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.FirmPortal;

/// <summary>
/// US8 / FR-050 — soft-lock-for-review handshake distinct from
/// TaxPeriod's hard close. The bookkeeper locks a month for review;
/// the accountant (firm user OR in-house) reviews + posts adjusting
/// JVs; either party releases when review is over.
///
/// At most one ACTIVE lock per (year, month) — enforced by the
/// filtered unique index `ux_period_review_locks_active_year_month`
/// on `(period_year, period_month) WHERE released_at_utc IS NULL`.
/// Re-locking after release works via a fresh row.
///
/// Each transition appends an FR-028 audit row so the chain shows
/// when the bookkeeper handed off + when review concluded + how
/// many adjusting actions the accountant posted in the window.
/// </summary>
public sealed class PeriodReviewLockHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public PeriodReviewLockHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<PeriodReviewLock> LockAsync(
        LockForReviewCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _db.Set<PeriodReviewLock>()
            .Where(l =>
                l.PeriodYear == command.PeriodYear
                && l.PeriodMonth == command.PeriodMonth
                && l.ReleasedAtUtc == null
            )
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Period {command.PeriodYear}-{command.PeriodMonth:D2} is already locked for review by user {existing.LockedByUserId:D} since {existing.LockedAtUtc:o}."
            );
        }

        var nowUtc = _clock.UtcNow;
        var review = new PeriodReviewLock(
            periodYear: command.PeriodYear,
            periodMonth: command.PeriodMonth,
            lockedAtUtc: nowUtc,
            lockedByUserId: command.LockedByUserId,
            lockedNote: command.Note
        );
        _db.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "period_review.locked",
                ActorUserId: command.LockedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"id":"{{review.Id:D}}","period_year":{{command.PeriodYear.ToString(CultureInfo.InvariantCulture)}},"period_month":{{command.PeriodMonth.ToString(CultureInfo.InvariantCulture)}},"locked_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}","note":{{(command.Note is null ? "null" : "\"" + Escape(command.Note) + "\"")}}}"""
            ),
            cancellationToken
        );

        return review;
    }

    public async Task<PeriodReviewLock> ReleaseAsync(
        ReleaseReviewCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var review =
            await _db.Set<PeriodReviewLock>()
                .FirstOrDefaultAsync(l => l.Id == command.PeriodReviewLockId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No review-lock found with id {command.PeriodReviewLockId:D}."
            );

        var alreadyReleased = review.ReleasedAtUtc is not null;
        var nowUtc = _clock.UtcNow;
        review.Release(nowUtc, command.ReleasedByUserId);
        await _db.SaveChangesAsync(cancellationToken);

        if (!alreadyReleased)
        {
            await _auditLog.AppendAsync(
                new AuditLogPayload(
                    Kind: "period_review.released",
                    ActorUserId: command.ReleasedByUserId,
                    ActorFirmName: null,
                    CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"id":"{{review.Id:D}}","period_year":{{review.PeriodYear.ToString(CultureInfo.InvariantCulture)}},"period_month":{{review.PeriodMonth.ToString(CultureInfo.InvariantCulture)}},"released_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}","accountant_actions_during_lock":{{review.AccountantActionsDuringLock.ToString(CultureInfo.InvariantCulture)}}}"""
                ),
                cancellationToken
            );
        }

        return review;
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
