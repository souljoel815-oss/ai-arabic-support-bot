using EgyptTax.Application.Compliance.PenaltyShield;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Compliance.PenaltyShield;

/// <summary>
/// EF-backed implementation of <see cref="IPenaltyExposureQuery"/>. One
/// query per metric (kept simple — counts on the EtaSubmission table
/// with composite indexes on Status + SubmissionWindowExpiresAtUtc).
/// </summary>
public sealed class SqlPenaltyExposureQuery : IPenaltyExposureQuery
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SqlPenaltyExposureQuery(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<PenaltyExposureReport> GetAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rollingFrom = nowUtc.AddDays(-PenaltyRegime.RollingWindowDays);
        var monthStart  = new DateTime(nowUtc.Year, nowUtc.Month, 1);
        var critCutoff  = nowUtc.AddHours(24);
        var sevenDays   = nowUtc.AddDays(7);

        // "Late" = the submission window has expired AND the doc is
        // not yet in Submitted state. Pending past deadline counts as
        // late even though no fine has crystallised on the regulator
        // side until they audit; the dashboard surfaces it as exposure.
        var lateBaseQuery = db.Set<EtaSubmission>().AsNoTracking()
            .Where(s => s.Status != EtaSubmissionStatus.Submitted
                && s.SubmissionWindowExpiresAtUtc < nowUtc);

        var lateInRollingWindow = await lateBaseQuery
            .Where(s => s.SubmissionWindowExpiresAtUtc >= rollingFrom)
            .CountAsync(ct);

        var lateThisMonth = await lateBaseQuery
            .Where(s => s.SubmissionWindowExpiresAtUtc >= monthStart)
            .CountAsync(ct);

        // Tier escalation per PenaltyRegime thresholds.
        PenaltyTier currentTier;
        if (lateInRollingWindow == 0)               currentTier = PenaltyTier.Clear;
        else if (lateInRollingWindow < PenaltyRegime.Tier3LateThreshold) currentTier = PenaltyTier.Tier2;
        else                                        currentTier = PenaltyTier.Tier3;

        // If the issuer is over the warning threshold but under Tier 2
        // entry, they're still in "Warning" (zero monetary fine yet).
        if (currentTier == PenaltyTier.Tier2 && lateInRollingWindow < PenaltyRegime.Tier2LateThreshold)
        {
            currentTier = PenaltyTier.Warning;
        }

        var projectedThisMonthEgp = currentTier switch
        {
            PenaltyTier.Tier2 => PenaltyRegime.ComputeTier2MonthFine(lateThisMonth),
            PenaltyTier.Tier3 => PenaltyRegime.ComputeTier3MonthFine(lateThisMonth),
            _ => 0m,
        };

        // "Avoidable if acted today" = the slice of currently-pending
        // submissions still inside their window. Submitting them now
        // keeps them out of the late bucket. Pending in window OR
        // failed in window count.
        var avoidableCount = await db.Set<EtaSubmission>().AsNoTracking()
            .Where(s => s.Status != EtaSubmissionStatus.Submitted
                && s.SubmissionWindowExpiresAtUtc >= nowUtc)
            .CountAsync(ct);
        var avoidableEgp = currentTier switch
        {
            PenaltyTier.Tier2 => avoidableCount * PenaltyRegime.Tier2PerInvoiceFineEgp,
            PenaltyTier.Tier3 => avoidableCount * PenaltyRegime.Tier3PerInvoiceFineEgp,
            // Even if currently Clear, slipping into Tier 2 starts
            // costing 5K each — show the worst-case avoidable amount
            // so the dashboard "sells" the value of acting early.
            _ => avoidableCount * PenaltyRegime.Tier2PerInvoiceFineEgp,
        };

        var criticalNext24h = await db.Set<EtaSubmission>().AsNoTracking()
            .Where(s => (s.Status == EtaSubmissionStatus.Pending || s.Status == EtaSubmissionStatus.Failed)
                && s.SubmissionWindowExpiresAtUtc >= nowUtc
                && s.SubmissionWindowExpiresAtUtc < critCutoff)
            .CountAsync(ct);

        var criticalNext7d = await db.Set<EtaSubmission>().AsNoTracking()
            .Where(s => (s.Status == EtaSubmissionStatus.Pending || s.Status == EtaSubmissionStatus.Failed)
                && s.SubmissionWindowExpiresAtUtc >= nowUtc
                && s.SubmissionWindowExpiresAtUtc < sevenDays)
            .CountAsync(ct);

        // Work queue — top 25 most-urgent items. Overdue first, then
        // closest-to-deadline.
        var queueRaw = await db.Set<EtaSubmission>().AsNoTracking()
            .Where(s => s.Status != EtaSubmissionStatus.Submitted)
            .OrderBy(s => s.SubmissionWindowExpiresAtUtc)
            .Take(25)
            .Join(
                db.Set<SalesInvoice>().AsNoTracking(),
                sub => sub.SalesInvoiceId,
                inv => inv.Id,
                (sub, inv) => new
                {
                    sub.SalesInvoiceId,
                    inv.DocumentNumber,
                    GrandTotal = inv.GrandTotal.Amount,
                    sub.SubmissionWindowExpiresAtUtc,
                })
            .ToListAsync(ct);

        var perInvoiceFine = currentTier switch
        {
            PenaltyTier.Tier3 => PenaltyRegime.Tier3PerInvoiceFineEgp,
            _                  => PenaltyRegime.Tier2PerInvoiceFineEgp,
        };

        var workQueue = queueRaw
            .Select(r =>
            {
                var hours = (r.SubmissionWindowExpiresAtUtc - nowUtc).TotalHours;
                var urgency =
                    hours < 0 ? PenaltyActionUrgency.Overdue
                    : hours < 24 ? PenaltyActionUrgency.Critical
                    : PenaltyActionUrgency.Soon;
                return new PenaltyActionItem(
                    SalesInvoiceId: r.SalesInvoiceId,
                    DocumentNumber: r.DocumentNumber,
                    GrandTotalEgp: r.GrandTotal,
                    DeadlineUtc: r.SubmissionWindowExpiresAtUtc,
                    HoursUntilDeadline: hours,
                    Urgency: urgency,
                    AvoidableFineEgp: urgency == PenaltyActionUrgency.Overdue ? 0m : perInvoiceFine);
            })
            .ToList();

        return new PenaltyExposureReport(
            AsOfUtc:                  nowUtc,
            CurrentTier:              currentTier,
            LateInRollingWindow:      lateInRollingWindow,
            LateThisMonth:            lateThisMonth,
            ProjectedThisMonthEgp:    projectedThisMonthEgp,
            AvoidableIfActedTodayEgp: avoidableEgp,
            CriticalNext24h:          criticalNext24h,
            CriticalNext7d:           criticalNext7d,
            WorkQueue:                workQueue);
    }
}
