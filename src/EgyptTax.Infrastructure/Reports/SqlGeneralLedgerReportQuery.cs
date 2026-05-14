using EgyptTax.Application.Reports;
using EgyptTax.Domain.Accounting;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Reports;

/// <summary>
/// v4 A.1 — EF-backed General Ledger. Materialises every
/// JournalEntryLine in the period (and every line strictly before
/// the period — for opening balances), then groups in memory by
/// AccountCode + sorts chronologically + accumulates the running
/// balance.
///
/// Why in-memory aggregation: SQLite's EF translator can't Sum()
/// decimals server-side (same constraint that drove the Trial
/// Balance to project + group locally). Volume is one period of
/// posted journal lines for an SMB — comfortably small.
/// </summary>
public sealed class SqlGeneralLedgerReportQuery : IGeneralLedgerReportQuery
{
    private readonly AppDbContext _db;

    public SqlGeneralLedgerReportQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> GetKnownAccountCodesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var codes = await _db.Set<JournalEntry>()
            .AsNoTracking()
            .SelectMany(e => e.Lines.Select(l => l.AccountCode))
            .Distinct()
            .ToListAsync(cancellationToken);
        codes.Sort(StringComparer.Ordinal);
        return codes;
    }

    public async Task<GeneralLedgerReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<string>? accountCodes,
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

        var requestedCodes = accountCodes is null || accountCodes.Count == 0
            ? null
            : accountCodes.ToHashSet(StringComparer.Ordinal);

        // Pull period lines + opening-balance lines (strictly before
        // the period) in one round-trip each. We project to a flat
        // anonymous shape so the JournalEntry/Line graph isn't
        // tracked + so the SUM happens in memory (SQLite limitation).
        var openingRaw = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc < startUtc
            select new
            {
                l.AccountCode,
                Debit = l.Debit.Amount,
                Credit = l.Credit.Amount,
            }
        ).ToListAsync(cancellationToken);

        var periodRaw = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc >= startUtc && e.PostedAtUtc < endExclusive
            select new
            {
                e.PostedAtUtc,
                e.SourceDocumentId,
                e.SourceDocumentNumber,
                e.SourceDocumentType,
                l.AccountCode,
                l.Description,
                Debit = l.Debit.Amount,
                Credit = l.Credit.Amount,
            }
        ).ToListAsync(cancellationToken);

        // Account codes touched by EITHER opening or period activity,
        // optionally filtered to the operator's selection.
        var touchedCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in openingRaw) touchedCodes.Add(r.AccountCode);
        foreach (var r in periodRaw) touchedCodes.Add(r.AccountCode);
        if (requestedCodes is not null)
        {
            touchedCodes.IntersectWith(requestedCodes);
        }

        var openingByCode = openingRaw
            .GroupBy(x => x.AccountCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Debit) - g.Sum(x => x.Credit),
                StringComparer.Ordinal
            );

        var periodByCode = periodRaw
            .GroupBy(x => x.AccountCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var sections = new List<GeneralLedgerSection>(touchedCodes.Count);
        decimal totalDebits = 0m;
        decimal totalCredits = 0m;

        foreach (var code in touchedCodes.OrderBy(c => c, StringComparer.Ordinal))
        {
            var opening = openingByCode.TryGetValue(code, out var o) ? o : 0m;
            var rows = periodByCode.TryGetValue(code, out var rs) ? rs : new();

            // Chronological order; tie-break by source-doc number for
            // deterministic display.
            rows.Sort(
                (a, b) =>
                {
                    var t = a.PostedAtUtc.CompareTo(b.PostedAtUtc);
                    return t != 0
                        ? t
                        : string.Compare(
                            a.SourceDocumentNumber,
                            b.SourceDocumentNumber,
                            StringComparison.Ordinal
                        );
                }
            );

            var running = opening;
            decimal periodDebit = 0m;
            decimal periodCredit = 0m;
            var entries = new List<GeneralLedgerEntry>(rows.Count);
            foreach (var r in rows)
            {
                running += r.Debit - r.Credit;
                periodDebit += r.Debit;
                periodCredit += r.Credit;
                entries.Add(
                    new GeneralLedgerEntry(
                        PostedAtUtc: r.PostedAtUtc,
                        SourceDocumentId: r.SourceDocumentId,
                        SourceDocumentNumber: r.SourceDocumentNumber,
                        SourceDocumentType: r.SourceDocumentType,
                        Description: r.Description ?? "",
                        Debit: MoneyEgp.From(decimal.Round(r.Debit, 2, MidpointRounding.ToEven)),
                        Credit: MoneyEgp.From(decimal.Round(r.Credit, 2, MidpointRounding.ToEven)),
                        RunningBalance: MoneyEgp.From(decimal.Round(running, 2, MidpointRounding.ToEven))
                    )
                );
            }

            totalDebits += periodDebit;
            totalCredits += periodCredit;

            sections.Add(
                new GeneralLedgerSection(
                    AccountCode: code,
                    OpeningBalance: MoneyEgp.From(decimal.Round(opening, 2, MidpointRounding.ToEven)),
                    Entries: entries,
                    PeriodDebitTotal: MoneyEgp.From(decimal.Round(periodDebit, 2, MidpointRounding.ToEven)),
                    PeriodCreditTotal: MoneyEgp.From(decimal.Round(periodCredit, 2, MidpointRounding.ToEven)),
                    ClosingBalance: MoneyEgp.From(decimal.Round(running, 2, MidpointRounding.ToEven))
                )
            );
        }

        return new GeneralLedgerReport(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            Sections: sections,
            TotalDebits: MoneyEgp.From(decimal.Round(totalDebits, 2, MidpointRounding.ToEven)),
            TotalCredits: MoneyEgp.From(decimal.Round(totalCredits, 2, MidpointRounding.ToEven))
        );
    }
}
