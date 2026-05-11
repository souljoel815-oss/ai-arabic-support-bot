using EgyptTax.Application.Wht;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Wht;

/// <summary>
/// P1.14 — EF-backed implementation of
/// <see cref="IInboundWhtMatcher"/>. Filters posted sales invoices
/// to the customer + period window, computes implied WHT per row
/// (grand_total × rate_applied / 100), and returns those within
/// the operator's tolerance, ranked by score (closeness to the
/// certificate's amount).
/// </summary>
public sealed class SqlInboundWhtMatcher : IInboundWhtMatcher
{
    private readonly AppDbContext _db;

    public SqlInboundWhtMatcher(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<InboundWhtMatchCandidate>> FindCandidatesAsync(
        Guid customerId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal amountWithheldEgp,
        decimal rateAppliedPercent,
        decimal tolerancePercent = 2.0m,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty) return Array.Empty<InboundWhtMatchCandidate>();
        if (rateAppliedPercent <= 0m) return Array.Empty<InboundWhtMatchCandidate>();

        var rows = await _db.Set<SalesInvoice>()
            .AsNoTracking()
            .Where(i =>
                i.CustomerId == customerId
                && i.State == DocumentState.Posted
                && i.DocumentDate >= periodStart
                && i.DocumentDate <= periodEnd)
            .Select(i => new
            {
                i.Id,
                i.DocumentNumber,
                i.DocumentDate,
                GrandTotalAmount = i.GrandTotal.Amount,
            })
            .ToListAsync(cancellationToken);

        var results = new List<InboundWhtMatchCandidate>(rows.Count);
        foreach (var r in rows)
        {
            var impliedWithholding = decimal.Round(
                r.GrandTotalAmount * rateAppliedPercent / 100m,
                2,
                MidpointRounding.ToEven);

            // Score = how close the implied amount is to the
            // certificate's amount. 100% on exact match; 0% when
            // the gap equals or exceeds the certificate amount.
            var gap = Math.Abs(impliedWithholding - amountWithheldEgp);
            if (amountWithheldEgp <= 0m) continue;
            var gapPct = gap / amountWithheldEgp * 100m;
            if (gapPct > tolerancePercent) continue;

            var score = decimal.Round(100m - gapPct, 2);
            results.Add(new InboundWhtMatchCandidate(
                SalesInvoiceId: r.Id,
                DocumentNumber: r.DocumentNumber ?? "",
                DocumentDate: r.DocumentDate,
                GrandTotal: MoneyEgp.From(r.GrandTotalAmount),
                ImpliedWithholding: MoneyEgp.From(impliedWithholding),
                ScorePercent: score));
        }

        return results
            .OrderByDescending(c => c.ScorePercent)
            .ThenBy(c => c.DocumentDate)
            .ToList();
    }
}
