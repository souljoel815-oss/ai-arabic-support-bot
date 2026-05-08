using EgyptTax.Application.Journals;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Documents;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Journals;

/// <summary>
/// FR-024 / FR-025 / T171 — EF-backed unified journal-ledger
/// query. Reads BOTH JournalEntry (auto-emitted) and JournalVoucher
/// (manual + reversal) within the period; merges them into a single
/// chronological list. The two underlying aggregates live in
/// separate tables (accounting.journal_entries +
/// accounting.journal_vouchers) — each carries different metadata
/// (auto-emitted has source-document linkage; manual carries
/// operator narration; reversal carries a back-pointer) — so the
/// query loads each set, projects to <see cref="JournalListRow"/>,
/// and orders the union.
/// </summary>
public sealed class SqlJournalLedgerQuery : IJournalLedgerQuery
{
    private readonly AppDbContext _db;

    public SqlJournalLedgerQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<JournalListRow>> ListAsync(
        DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException(
                $"PeriodEnd ({periodEnd:yyyy-MM-dd}) cannot be before PeriodStart ({periodStart:yyyy-MM-dd}).",
                nameof(periodEnd));
        }

        var startUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var endExclusiveUtc = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var startDate = periodStart;
        var endDate = periodEnd;

        // Auto-emitted (JournalEntry). Project to row in-memory after
        // loading the lines so we can sum debits without a second
        // query per entry.
        var autoEntries = await _db.Set<JournalEntry>().AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.PostedAtUtc >= startUtc && e.PostedAtUtc < endExclusiveUtc)
            .ToListAsync(cancellationToken);

        var autoRows = autoEntries.Select(e => new JournalListRow(
            Id: e.Id,
            Kind: JournalEntryKind.AutoEmitted,
            PostedAtUtc: e.PostedAtUtc,
            SourceLabel: $"{e.SourceDocumentType} {e.SourceDocumentNumber}",
            SourceDocumentType: e.SourceDocumentType,
            SourceDocumentId: e.SourceDocumentId,
            SourceDocumentNumber: e.SourceDocumentNumber,
            TotalDebits: e.Lines.Sum(l => l.Debit.Amount),
            LineCount: e.Lines.Count));

        // Manual + reversal (JournalVoucher). Period filter on
        // voucher.Date (the operator-supplied effective date) rather
        // than CreatedAtUtc so manual back-dated vouchers land in
        // the right period.
        var vouchers = await _db.Set<JournalVoucher>().AsNoTracking()
            .Include(v => v.Lines)
            .Where(v => v.Date >= startDate && v.Date <= endDate)
            .ToListAsync(cancellationToken);

        var voucherRows = vouchers.Select(v => new JournalListRow(
            Id: v.Id,
            Kind: v.ReversesJournalVoucherId is not null ? JournalEntryKind.Reversal : JournalEntryKind.ManualAdjusting,
            PostedAtUtc: v.CreatedAtUtc,
            SourceLabel: v.ReversesJournalVoucherId is not null
                ? $"Reversal of voucher {v.ReversesJournalVoucherId:D}"
                : $"Manual: {v.Narration.English}",
            SourceDocumentType: v.SourceDocumentType,
            SourceDocumentId: v.SourceDocumentId,
            SourceDocumentNumber: null,
            TotalDebits: v.Lines.Sum(l => l.Debit.Amount),
            LineCount: v.Lines.Count));

        return autoRows.Concat(voucherRows)
            .OrderBy(r => r.PostedAtUtc)
            .ThenBy(r => r.SourceLabel, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<JournalDetailDto?> GetAsync(
        Guid id, JournalEntryKind kind, CancellationToken cancellationToken = default)
    {
        if (kind == JournalEntryKind.AutoEmitted)
        {
            var entry = await _db.Set<JournalEntry>().AsNoTracking()
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (entry is null) return null;

            return new JournalDetailDto(
                Id: entry.Id,
                Kind: JournalEntryKind.AutoEmitted,
                PostedAtUtc: entry.PostedAtUtc,
                Narration: $"Auto-emitted on Post of {entry.SourceDocumentType} {entry.SourceDocumentNumber}",
                SourceDocumentType: entry.SourceDocumentType,
                SourceDocumentId: entry.SourceDocumentId,
                SourceDocumentNumber: entry.SourceDocumentNumber,
                ReversesJournalVoucherId: null,
                TotalDebits: entry.Lines.Sum(l => l.Debit.Amount),
                TotalCredits: entry.Lines.Sum(l => l.Credit.Amount),
                Lines: entry.Lines.Select(l => new JournalDetailLineDto(
                    l.AccountCode, l.Debit.Amount, l.Credit.Amount, l.Description)).ToList());
        }

        // Manual or Reversal — both stored in JournalVoucher.
        var voucher = await _db.Set<JournalVoucher>().AsNoTracking()
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (voucher is null) return null;

        var resolvedKind = voucher.ReversesJournalVoucherId is not null
            ? JournalEntryKind.Reversal
            : JournalEntryKind.ManualAdjusting;

        return new JournalDetailDto(
            Id: voucher.Id,
            Kind: resolvedKind,
            PostedAtUtc: voucher.CreatedAtUtc,
            Narration: voucher.Narration.English,
            SourceDocumentType: voucher.SourceDocumentType,
            SourceDocumentId: voucher.SourceDocumentId,
            SourceDocumentNumber: null,
            ReversesJournalVoucherId: voucher.ReversesJournalVoucherId,
            TotalDebits: voucher.Lines.Sum(l => l.Debit.Amount),
            TotalCredits: voucher.Lines.Sum(l => l.Credit.Amount),
            Lines: voucher.Lines.Select(l => new JournalDetailLineDto(
                l.AccountCode, l.Debit.Amount, l.Credit.Amount, l.Description)).ToList());
    }
}
