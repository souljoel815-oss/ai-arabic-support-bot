using EgyptTax.Application.Banking;
using EgyptTax.Domain.Banking;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// P3.4 — Hangfire job that runs <see cref="BankMatchScorer"/>
/// against every Unmatched <see cref="BankStatementLine"/> and
/// either:
///   • auto-matches (Score ≥ 95) — flips Status straight to
///     <see cref="BankStatementLineStatus.Matched"/>; or
///   • suggests (70 ≤ Score &lt; 95) — flips Status to
///     <see cref="BankStatementLineStatus.SuggestedMatch"/> for
///     human review in the unmatched queue.
///
/// Idempotent: if a line is already matched/ignored/suggested the
/// job leaves it alone (only Unmatched lines get touched). The
/// scorer is pure, so re-running with no new vouchers is a no-op.
/// </summary>
public sealed class BankAutoMatchJob
{
    /// <summary>System actor id for auto-generated bookkeeping —
    /// shared shape with <see cref="MonthlyDepreciationJob.SystemActorId"/>
    /// so the audit trail filter works the same.</summary>
    public static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000002");

    /// <summary>How many unmatched lines to chew through per tick.
    /// Cap exists so a freshly-imported 5,000-line statement doesn't
    /// hold the DB context for an unbounded time; subsequent ticks
    /// catch up.</summary>
    public const int LinesPerTick = 200;

    /// <summary>Voucher candidate window — pulls the last 90 days
    /// worth of posted vouchers per tick. Anything older than that
    /// in unmatched bank lines is too cold to auto-match
    /// confidently and should land via the manual queue.</summary>
    public const int CandidateLookbackDays = 90;

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<BankAutoMatchJob> _logger;

    public BankAutoMatchJob(
        AppDbContext db,
        IClock clock,
        ILogger<BankAutoMatchJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BankAutoMatchResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        var lookbackCutoff = DateOnly.FromDateTime(nowUtc).AddDays(-CandidateLookbackDays);

        var lines = await _db.Set<BankStatementLine>()
            .Where(l => l.Status == BankStatementLineStatus.Unmatched)
            .OrderBy(l => l.TransactionDate)
            .Take(LinesPerTick)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return new BankAutoMatchResult(0, 0, 0);
        }

        // Pull voucher candidates once per tick — much cheaper than
        // re-querying per line. Restricted to posted vouchers in the
        // lookback window.
        var spvs = await _db.Set<SupplierPaymentVoucher>()
            .AsNoTracking()
            .Where(v => v.State == DocumentState.Posted && v.PaymentDate >= lookbackCutoff)
            .ToListAsync(cancellationToken);
        var crvs = await _db.Set<CustomerReceiptVoucher>()
            .AsNoTracking()
            .Where(v => v.State == DocumentState.Posted && v.ReceiptDate >= lookbackCutoff)
            .ToListAsync(cancellationToken);

        var supplierIds = spvs.Select(v => v.SupplierId).Distinct().ToArray();
        var suppliers = await _db.Set<Supplier>()
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var customerIds = crvs.Select(v => v.CustomerId).Distinct().ToArray();
        var customers = await _db.Set<Customer>()
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var autoMatched = 0;
        var suggested = 0;
        var noMatch = 0;

        foreach (var line in lines)
        {
            BankMatchScorer.ScoredCandidate? best;
            if (line.Debit.Amount > 0m)
            {
                best = BankMatchScorer.ScoreOutflow(line, spvs, suppliers);
            }
            else if (line.Credit.Amount > 0m)
            {
                best = BankMatchScorer.ScoreInflow(line, crvs, customers);
            }
            else
            {
                noMatch++;
                continue;
            }

            if (best is null || best.Score < BankMatchScorer.SuggestionThreshold)
            {
                noMatch++;
                continue;
            }

            // Suggest first; auto-promote to Matched if confident.
            if (best.SupplierPaymentVoucherId is { } spvId)
            {
                line.SuggestSupplierPayment(spvId, best.Score, nowUtc);
            }
            else if (best.CustomerReceiptVoucherId is { } crvId)
            {
                line.SuggestCustomerReceipt(crvId, best.Score, nowUtc);
            }

            if (best.Score >= BankMatchScorer.AutoMatchThreshold)
            {
                line.AcceptSuggestion(SystemActorId, nowUtc);
                autoMatched++;
            }
            else
            {
                suggested++;
            }
        }

        if (autoMatched + suggested > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "BankAutoMatchJob — scanned {Lines} unmatched lines, auto-matched {Auto}, suggested {Suggested}, no match {NoMatch}.",
                lines.Count, autoMatched, suggested, noMatch);
        }

        return new BankAutoMatchResult(autoMatched, suggested, noMatch);
    }
}

public sealed record BankAutoMatchResult(int AutoMatched, int Suggested, int NoMatch);
