using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Compliance;
using EgyptTax.Application.Periods;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Periods;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Periods;

/// <summary>
/// FR-037 / P3.6 — Administrator-only lock + reopen. Find-or-create
/// on the natural key (kind, year, month_or_quarter); audits both
/// transitions so the FR-028 chain shows when the period closed
/// and when (if ever) it reopened.
///
/// P3.6: VAT-month locks now run through
/// <see cref="PeriodLockReadinessGate"/> first — hard blockers
/// (drafts, failed ETA submissions) refuse the command outright;
/// soft blockers (missing attachments) require an Administrator-
/// supplied <see cref="LockTaxPeriodCommand.ForceLockReason"/>.
/// IncomeFiscalYear / WhtQuarter locks bypass the gate (their
/// readiness checks ship with their respective filings; gating
/// here would require a different cockpit).
/// </summary>
public sealed class LockTaxPeriodHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;
    private readonly IMonthlyTaxClosingCockpitQuery _cockpitQuery;

    public LockTaxPeriodHandler(
        AppDbContext db,
        IClock clock,
        IAuditLogStore auditLog,
        IMonthlyTaxClosingCockpitQuery cockpitQuery)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
        _cockpitQuery = cockpitQuery;
    }

    public async Task<TaxPeriod> HandleAsync(
        LockTaxPeriodCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        // P3.6 — gate VAT-month locks against the cockpit. Other
        // period kinds skip the gate; those filings have their own
        // readiness paths and the cockpit query only supports months.
        var forceRequested = !string.IsNullOrWhiteSpace(command.ForceLockReason);
        PeriodLockReadinessDecision? gateDecision = null;
        if (command.PeriodKind == TaxPeriodKind.VatMonth)
        {
            var cockpit = await _cockpitQuery.RunAsync(
                command.Year,
                command.MonthOrQuarter,
                cancellationToken);
            gateDecision = PeriodLockReadinessGate.Evaluate(cockpit, forceRequested);
            if (!gateDecision.Allowed)
            {
                throw new PeriodLockBlockedException(gateDecision);
            }
        }

        var period = await _db.Set<TaxPeriod>()
            .FirstOrDefaultAsync(
                p =>
                    p.PeriodKind == command.PeriodKind
                    && p.Year == command.Year
                    && p.MonthOrQuarter == command.MonthOrQuarter,
                cancellationToken
            );
        if (period is null)
        {
            period = new TaxPeriod(command.PeriodKind, command.Year, command.MonthOrQuarter);
            _db.Add(period);
        }

        var nowUtc = _clock.UtcNow;
        period.Lock(command.LockedByUserId, nowUtc, command.Reason);
        await _db.SaveChangesAsync(cancellationToken);

        // Force-locks get a distinct audit kind so a downstream
        // auditor's "was anything force-locked?" filter is a single
        // index hit. Soft-blocker counts at decision time are baked
        // into the payload — the cockpit-snapshot is ephemeral so
        // the audit is the only durable record.
        var wasForced = forceRequested
            && gateDecision is { RequiresForce: true };
        var auditKind = wasForced ? "tax_period.locked.forced" : "tax_period.locked";
        var softCounts = wasForced && gateDecision is not null
            ? string.Join(",", gateDecision.SoftBlockers.Select(b =>
                $"\"{Escape(b.Code)}\":{b.Count.ToString(CultureInfo.InvariantCulture)}"))
            : "";
        var forceFragment = wasForced
            ? $",\"force_reason\":\"{Escape(command.ForceLockReason!)}\",\"soft_blockers\":{{{softCounts}}}"
            : "";

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: auditKind,
                ActorUserId: command.LockedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"period_id":"{{period.Id:D}}","period_kind":"{{command.PeriodKind}}","year":{{command.Year.ToString(CultureInfo.InvariantCulture)}},"month_or_quarter":{{command.MonthOrQuarter.ToString(CultureInfo.InvariantCulture)}},"locked_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}","reason":{{(command.Reason is null ? "null" : "\"" + Escape(command.Reason) + "\"")}}{{forceFragment}}}"""
            ),
            cancellationToken
        );

        return period;
    }

    public async Task<TaxPeriod> ReopenAsync(
        ReopenTaxPeriodCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var period =
            await _db.Set<TaxPeriod>()
                .FirstOrDefaultAsync(
                    p =>
                        p.PeriodKind == command.PeriodKind
                        && p.Year == command.Year
                        && p.MonthOrQuarter == command.MonthOrQuarter,
                    cancellationToken
                )
            ?? throw new InvalidOperationException(
                $"No tax period exists for {command.PeriodKind} {command.Year}-{command.MonthOrQuarter:D2} — nothing to reopen."
            );

        var nowUtc = _clock.UtcNow;
        period.Reopen(command.ReopenedByUserId, nowUtc, command.Reason);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "tax_period.reopened",
                ActorUserId: command.ReopenedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"period_id":"{{period.Id:D}}","period_kind":"{{command.PeriodKind}}","year":{{command.Year.ToString(CultureInfo.InvariantCulture)}},"month_or_quarter":{{command.MonthOrQuarter.ToString(CultureInfo.InvariantCulture)}},"reopened_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}","reason":{{(command.Reason is null ? "null" : "\"" + Escape(command.Reason) + "\"")}}}"""
            ),
            cancellationToken
        );

        return period;
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
