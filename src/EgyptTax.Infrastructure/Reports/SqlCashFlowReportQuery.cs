using EgyptTax.Application.Reports;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Reports;

/// <summary>
/// v4 B.4 — EF-backed Cash Flow query. Identifies "cash" accounts
/// by combining the legacy <c>1100</c> code with every registered
/// <c>CashAccount.AccountCode</c>; sums all journal-entry-line
/// activity hitting those codes for the period (per-line) +
/// strictly before the period (opening balance).
///
/// Buckets each in-period line by the parent JournalEntry's
/// SourceDocumentType:
///   * CustomerReceiptVoucher → CustomerReceipts
///   * SupplierPaymentVoucher → SupplierPayments
///   * everything else        → OtherMovements
///
/// Sign convention on the line amount: debit−credit, so a
/// CustomerReceipt comes out positive (debit cash) and a
/// SupplierPayment comes out negative (credit cash). The page can
/// render the value directly without re-flipping signs.
/// </summary>
public sealed class SqlCashFlowReportQuery : ICashFlowReportQuery
{
    private readonly AppDbContext _db;

    public SqlCashFlowReportQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CashFlowReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default
    )
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException(
                $"PeriodEnd ({periodEnd:yyyy-MM-dd}) cannot be before PeriodStart ({periodStart:yyyy-MM-dd}).",
                nameof(periodEnd)
            );
        }

        var startUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var endExclusive = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Build the cash-account code set: legacy "1100" + every
        // registered CashAccount.AccountCode (active or not — historic
        // payments may reference a deactivated account).
        var registeredCodes = await _db.Set<CashAccount>().AsNoTracking()
            .Select(a => a.AccountCode)
            .ToListAsync(cancellationToken);
        var cashCodes = registeredCodes
            .Append("1100")
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        // Pull all cash-touching JE lines (opening + period) in one
        // round-trip each. Project to anonymous to keep the JE/Line
        // graph untracked + sums in-memory (SQLite decimal-Sum
        // limitation; same shape as Trial Balance + GL).
        var openingRaw = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc < startUtc && cashCodes.Contains(l.AccountCode)
            select new
            {
                Debit = l.Debit.Amount,
                Credit = l.Credit.Amount,
            }
        ).ToListAsync(cancellationToken);

        var periodRaw = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc >= startUtc
                && e.PostedAtUtc < endExclusive
                && cashCodes.Contains(l.AccountCode)
            select new
            {
                e.PostedAtUtc,
                e.SourceDocumentId,
                e.SourceDocumentNumber,
                e.SourceDocumentType,
                l.Description,
                Debit = l.Debit.Amount,
                Credit = l.Credit.Amount,
            }
        ).ToListAsync(cancellationToken);

        var opening = openingRaw.Sum(x => x.Debit) - openingRaw.Sum(x => x.Credit);

        // For each in-period line, signed amount = debit − credit.
        // Group by the source document so a multi-line CRV/SPV that
        // happens to touch cash twice still surfaces as one row.
        var grouped = periodRaw
            .GroupBy(x => new
            {
                x.SourceDocumentId,
                x.SourceDocumentNumber,
                x.SourceDocumentType,
            })
            .Select(g =>
            {
                var earliest = g.Min(x => x.PostedAtUtc);
                var amount = g.Sum(x => x.Debit) - g.Sum(x => x.Credit);
                // Best-effort description: the first line's text is
                // usually the "cash leg" / canonical label.
                var description = g.OrderBy(x => x.PostedAtUtc)
                    .Select(x => x.Description)
                    .FirstOrDefault() ?? "";
                return new CashFlowLine(
                    PostedAtUtc: earliest,
                    SourceDocumentId: g.Key.SourceDocumentId,
                    SourceDocumentNumber: g.Key.SourceDocumentNumber,
                    SourceDocumentType: g.Key.SourceDocumentType,
                    Description: description,
                    Amount: MoneyEgp.From(decimal.Round(amount, 2, MidpointRounding.ToEven))
                );
            })
            .OrderBy(x => x.PostedAtUtc)
            .ThenBy(x => x.SourceDocumentNumber, StringComparer.Ordinal)
            .ToList();

        var customerReceipts = grouped
            .Where(x => x.SourceDocumentType == DocumentType.CustomerReceiptVoucher)
            .ToList();
        var supplierPayments = grouped
            .Where(x => x.SourceDocumentType == DocumentType.SupplierPaymentVoucher)
            .ToList();
        var otherMovements = grouped
            .Where(x => x.SourceDocumentType is not DocumentType.CustomerReceiptVoucher
                && x.SourceDocumentType is not DocumentType.SupplierPaymentVoucher)
            .ToList();

        var net = customerReceipts.Sum(x => x.Amount.Amount)
            + supplierPayments.Sum(x => x.Amount.Amount)
            + otherMovements.Sum(x => x.Amount.Amount);
        var closing = opening + net;

        return new CashFlowReport(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            OpeningCash: MoneyEgp.From(decimal.Round(opening, 2, MidpointRounding.ToEven)),
            CustomerReceipts: customerReceipts,
            SupplierPayments: supplierPayments,
            OtherMovements: otherMovements,
            NetCashChange: MoneyEgp.From(decimal.Round(net, 2, MidpointRounding.ToEven)),
            ClosingCash: MoneyEgp.From(decimal.Round(closing, 2, MidpointRounding.ToEven))
        );
    }
}
