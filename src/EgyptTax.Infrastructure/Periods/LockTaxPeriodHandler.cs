using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Periods;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Periods;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Periods;

/// <summary>
/// FR-037 — Administrator-only lock + reopen. Find-or-create on
/// the natural key (kind, year, month_or_quarter); audits both
/// transitions so the FR-028 chain shows when the period closed
/// and when (if ever) it reopened.
/// </summary>
public sealed class LockTaxPeriodHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public LockTaxPeriodHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<TaxPeriod> HandleAsync(LockTaxPeriodCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var period = await _db.Set<TaxPeriod>()
            .FirstOrDefaultAsync(p =>
                p.PeriodKind == command.PeriodKind
                && p.Year == command.Year
                && p.MonthOrQuarter == command.MonthOrQuarter, cancellationToken);
        if (period is null)
        {
            period = new TaxPeriod(command.PeriodKind, command.Year, command.MonthOrQuarter);
            _db.Add(period);
        }

        var nowUtc = _clock.UtcNow;
        period.Lock(command.LockedByUserId, nowUtc, command.Reason);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(new AuditLogPayload(
            Kind: "tax_period.locked",
            ActorUserId: command.LockedByUserId, ActorFirmName: null, CompanyId: Guid.Empty,
            PayloadJson: $$"""{"period_id":"{{period.Id:D}}","period_kind":"{{command.PeriodKind}}","year":{{command.Year.ToString(CultureInfo.InvariantCulture)}},"month_or_quarter":{{command.MonthOrQuarter.ToString(CultureInfo.InvariantCulture)}},"locked_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}","reason":{{(command.Reason is null ? "null" : "\"" + Escape(command.Reason) + "\"")}}}"""),
            cancellationToken);

        return period;
    }

    public async Task<TaxPeriod> ReopenAsync(ReopenTaxPeriodCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var period = await _db.Set<TaxPeriod>()
            .FirstOrDefaultAsync(p =>
                p.PeriodKind == command.PeriodKind
                && p.Year == command.Year
                && p.MonthOrQuarter == command.MonthOrQuarter, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No tax period exists for {command.PeriodKind} {command.Year}-{command.MonthOrQuarter:D2} — nothing to reopen.");

        var nowUtc = _clock.UtcNow;
        period.Reopen(command.ReopenedByUserId, nowUtc, command.Reason);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(new AuditLogPayload(
            Kind: "tax_period.reopened",
            ActorUserId: command.ReopenedByUserId, ActorFirmName: null, CompanyId: Guid.Empty,
            PayloadJson: $$"""{"period_id":"{{period.Id:D}}","period_kind":"{{command.PeriodKind}}","year":{{command.Year.ToString(CultureInfo.InvariantCulture)}},"month_or_quarter":{{command.MonthOrQuarter.ToString(CultureInfo.InvariantCulture)}},"reopened_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}","reason":{{(command.Reason is null ? "null" : "\"" + Escape(command.Reason) + "\"")}}}"""),
            cancellationToken);

        return period;
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);
}
