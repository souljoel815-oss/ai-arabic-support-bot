using EgyptTax.Application.Reports;
using EgyptTax.Domain.Periods;
using EgyptTax.Domain.Tax;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Tax;

/// <summary>
/// G3.3 — produces a <see cref="VatReturn"/> from a Locked tax
/// period. Same gate pattern as Form 41 (T154): refuses if the
/// VAT month isn't Locked yet, so drafts / missing-attachment /
/// failed-ETA noise can't leak into the return.
///
/// Re-generation produces a NEW row (audit trail keeps old ones).
/// </summary>
public sealed class GenerateVatReturnHandler
{
    private readonly AppDbContext _db;
    private readonly IVatMonthlyReportQuery _vatReport;
    private readonly IClock _clock;

    public GenerateVatReturnHandler(
        AppDbContext db,
        IVatMonthlyReportQuery vatReport,
        IClock clock)
    {
        _db = db;
        _vatReport = vatReport;
        _clock = clock;
    }

    public async Task<VatReturn> GenerateAsync(
        GenerateVatReturnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.GeneratedByUserId == Guid.Empty)
            throw new ArgumentException("GeneratedByUserId required.", nameof(command));

        // Period-lock gate — borrowed verbatim from Form 41 generator.
        // Refuses when the underlying tax period is still Open. We only
        // gate the VAT-monthly kind for v1; quarterly returns (Law 6)
        // would need their own TaxPeriod entries (deferred).
        var taxPeriod = await _db.Set<TaxPeriod>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.PeriodKind == TaxPeriodKind.VatMonth
                && p.Year == command.PeriodYear
                && p.MonthOrQuarter == command.PeriodOrdinal,
                cancellationToken);

        if (command.PeriodKind == VatReturnPeriodKind.VatMonthly)
        {
            if (taxPeriod is null || taxPeriod.Status != TaxPeriodStatus.Locked)
            {
                throw new InvalidOperationException(
                    $"Cannot generate VAT return for {command.PeriodYear}-{command.PeriodOrdinal:D2}: " +
                    "the VAT period is not Locked. Close the month via the Closing Cockpit first.");
            }
        }

        // Pull the monthly numbers (the existing query already covers
        // FR-020 input-VAT-recoverability rules). For the quarterly
        // regime we'd aggregate three monthly runs — v1 ships monthly
        // only; quarterly is a v1.1 follow-on.
        var report = await _vatReport.RunAsync(command.PeriodYear, command.PeriodOrdinal, cancellationToken);

        // Non-recoverable input VAT = input VAT on lines that DIDN'T
        // make it into InputVatRecoverable. The existing
        // VatMonthlyReportRow set has ContributesToOutput; total
        // input-VAT-paid is the sum of input rows; non-recoverable
        // is total-input − recoverable.
        var inputRowsTotalVat = report.Rows
            .Where(r => !r.ContributesToOutput)
            .Sum(r => r.VatAmount.Amount);
        var nonRecoverableInputVat = MoneyEgp.From(
            inputRowsTotalVat - report.InputVatRecoverable.Amount);

        var nowUtc = _clock.UtcNow;
        var vatReturn = new VatReturn(
            periodKind: command.PeriodKind,
            periodYear: command.PeriodYear,
            periodOrdinal: command.PeriodOrdinal,
            periodStart: report.PeriodStart,
            periodEnd: report.PeriodEnd,
            outputVat: report.OutputVat,
            inputVatRecoverable: report.InputVatRecoverable,
            inputVatNonRecoverable: nonRecoverableInputVat,
            netPayable: report.NetPayable,
            contributingDocumentCount: report.Rows.Count,
            generatedAtUtc: nowUtc,
            generatedByUserId: command.GeneratedByUserId);

        if (!string.IsNullOrWhiteSpace(command.Note))
        {
            vatReturn.UpdateNote(command.Note);
        }

        _db.Add(vatReturn);
        await _db.SaveChangesAsync(cancellationToken);
        return vatReturn;
    }
}

public sealed record GenerateVatReturnCommand(
    VatReturnPeriodKind PeriodKind,
    int PeriodYear,
    int PeriodOrdinal,
    Guid GeneratedByUserId,
    string? Note = null);
