using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Wht;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Tax;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Wht;

/// <summary>
/// FR-046 / T210 / US7 scenario 3 — transition a Form 41 filing
/// from Unfiled → Filed. Three things happen on success:
///
///   1. Re-runs the reconciliation (cert total in the quarter vs
///      WHT-payable account accrual) — refuses to file dirty.
///   2. Stamps every WhtCertificate in the quarter with the
///      filing's id via <c>MarkIncludedInForm41Filing</c> —
///      enforces T200 immutability (a stamped cert can never be
///      included in another filing).
///   3. Calls <c>filing.MarkFiled(user, when)</c> which transitions
///      the entity status + records filed-at + filed-by; entity
///      itself refuses a second call.
///
/// All three commit in the same SaveChangesAsync envelope so the
/// transition + the certificate stamps land atomically. The
/// operator's audit trail captures the action.
/// </summary>
public sealed class MarkForm41FiledHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public MarkForm41FiledHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<Form41Filing> HandleAsync(
        MarkForm41FiledCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var filing =
            await _db.Set<Form41Filing>()
                .FirstOrDefaultAsync(f => f.Id == command.Form41FilingId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Form41Filing {command.Form41FilingId} not found."
            );

        // Early status check — short-circuit before recomputing the
        // reconciliation. On a second call the certs would all be
        // stamped already, the reconciliation would falsely fail
        // (cert total = 0), and the operator would see a misleading
        // "reconciliation" error instead of the actual "already
        // Filed" reason.
        if (filing.Status == Form41Status.Filed)
        {
            throw new InvalidOperationException(
                $"Form 41 for {filing.FiscalYear}-Q{filing.Quarter} is already Filed at {filing.FiledAtUtc:yyyy-MM-dd HH:mm:ss}; cannot mark it filed twice (FR-046)."
            );
        }

        // Quarter date range — same math the generator used.
        var (periodStart, periodEnd) = QuarterDates(filing.FiscalYear, filing.Quarter);
        var startUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var endExclusive = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Re-run the reconciliation against current state. Cert
        // total includes any cert in the quarter that isn't already
        // stamped — combined with the (future) regenerate-then-mark
        // workflow, this catches operators who try to file an
        // out-of-date payload after issuing more certs since
        // generation.
        var certs = await _db.Set<WhtCertificate>()
            .Where(c =>
                c.Direction == WhtCertificateDirection.OutboundToSupplier
                && c.Date >= periodStart
                && c.Date <= periodEnd
                && c.IncludedInForm41FilingId == null
            )
            .ToListAsync(cancellationToken);
        var certTotal = certs.Sum(c => c.AmountWithheld.Amount);

        // Same dual-source accrual the generator uses (auto-emitted
        // JournalEntry + manual JournalVoucher). Keeps the
        // reconciliation comparable across the two callers.
        // SQLite portable-mode workaround: server-side decimal Sum
        // is unsupported, so materialise + sum in-memory. Volume is
        // bounded by the period filter.
        var jeAmounts = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where
                e.PostedAtUtc >= startUtc
                && e.PostedAtUtc < endExclusive
                && l.AccountCode == ChartOfAccountCodes.WhtPayable
            select l.Credit.Amount - l.Debit.Amount
        ).ToListAsync(cancellationToken);
        var jvAmounts = await (
            from v in _db.Set<EgyptTax.Domain.Documents.JournalVoucher>().AsNoTracking()
            from l in v.Lines
            where
                v.Date >= periodStart
                && v.Date <= periodEnd
                && l.AccountCode == ChartOfAccountCodes.WhtPayable
            select l.Credit.Amount - l.Debit.Amount
        ).ToListAsync(cancellationToken);
        var whtPayableAccrued = jeAmounts.Sum() + jvAmounts.Sum();

        if (whtPayableAccrued != certTotal)
        {
            throw new InvalidOperationException(
                $"Cannot mark Form 41 for {filing.FiscalYear}-Q{filing.Quarter} as Filed: "
                    + $"reconciliation does NOT match. Cert total {certTotal:F2} EGP vs WHT-payable accrual "
                    + $"{whtPayableAccrued:F2} EGP (discrepancy {(whtPayableAccrued - certTotal):F2}). "
                    + "FR-046 — fix the books (likely a missing manual adjusting voucher), regenerate, then re-file."
            );
        }

        var nowUtc = _clock.UtcNow;

        // Stamp every cert with the filing id. Each call refuses
        // if already stamped — defensive, since the WHERE clause
        // above already filters them out.
        foreach (var cert in certs)
        {
            cert.MarkIncludedInForm41Filing(filing.Id);
        }

        // Transition the entity. Refuses a second call (FR-046 —
        // one canonical filing per quarter, immutable once filed).
        filing.MarkFiled(command.FiledByUserId, nowUtc);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "form41.marked_filed",
                ActorUserId: command.FiledByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"form41_filing_id":"{{filing.Id:D}}","fiscal_year":{{filing.FiscalYear}},"quarter":{{filing.Quarter}},"line_count":{{certs.Count}},"total_wht_egp":{{certTotal.ToString("F2", CultureInfo.InvariantCulture)}},"filed_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}"}"""
            ),
            cancellationToken
        );

        return filing;
    }

    private static (DateOnly Start, DateOnly End) QuarterDates(int fiscalYear, int quarter)
    {
        var startMonth = (quarter - 1) * 3 + 1;
        var endMonth = startMonth + 2;
        var start = new DateOnly(fiscalYear, startMonth, 1);
        var end = new DateOnly(fiscalYear, endMonth, DateTime.DaysInMonth(fiscalYear, endMonth));
        return (start, end);
    }
}
