using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// v5 — pre-rolls a tiny "what's happening in the books right now"
/// snapshot that <see cref="NlQueryHandler"/> embeds in the system
/// prompt before every chat question. The model can then quote
/// real figures ("you have 7 draft invoices worth 12,300 EGP")
/// instead of saying "open /invoices?state=Draft to see the count".
///
/// All queries are intentionally cheap aggregates against the
/// active tenant's data — single round-trip each, no joins beyond
/// the customer name lookup. The result is cached per-AppDbContext
/// call (no time-based cache) so each chat ask gets fresh numbers
/// without re-running across the same handler scope.
///
/// Numbers are factual snapshots, NOT calculations the model
/// invents. The system prompt is updated to tell the model that
/// quoting these numbers verbatim is allowed; calculating new
/// numbers from them is still forbidden.
/// </summary>
public sealed class AiContextSnapshotProvider
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IClock _clock;
    private readonly ILogger<AiContextSnapshotProvider> _log;

    public AiContextSnapshotProvider(
        IDbContextFactory<AppDbContext> dbFactory,
        IClock clock,
        ILogger<AiContextSnapshotProvider> log)
    {
        _dbFactory = dbFactory;
        _clock = clock;
        _log = log;
    }

    public async Task<AiContextSnapshot> BuildAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _clock.UtcNow;
            var monthStart = new DateOnly(now.Year, now.Month, 1);

            var draftSalesCount = await db.Set<SalesInvoice>().AsNoTracking()
                .CountAsync(i => i.State == DocumentState.Draft, ct);

            var postedThisMonth = await db.Set<SalesInvoice>().AsNoTracking()
                .Where(i => i.State == DocumentState.Posted && i.DocumentDate >= monthStart)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Total = g.Sum(x => x.GrandTotal.Amount),
                    Vat = g.Sum(x => x.VatTotal.Amount),
                })
                .FirstOrDefaultAsync(ct);

            var purchasesThisMonth = await db.Set<PurchaseInvoice>().AsNoTracking()
                .Where(p => p.State == DocumentState.Posted && p.DateReceived >= monthStart)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Total = g.Sum(x => x.GrandTotal.Amount),
                    Vat = g.Sum(x => x.VatTotal.Amount),
                })
                .FirstOrDefaultAsync(ct);

            var draftPurchaseCount = await db.Set<PurchaseInvoice>().AsNoTracking()
                .CountAsync(p => p.State == DocumentState.Draft, ct);

            return new AiContextSnapshot(
                CapturedAtUtc: now,
                DraftSalesCount: draftSalesCount,
                DraftPurchasesCount: draftPurchaseCount,
                MonthSalesCount: postedThisMonth?.Count ?? 0,
                MonthSalesTotalEgp: postedThisMonth?.Total ?? 0m,
                MonthVatOutputEgp: postedThisMonth?.Vat ?? 0m,
                MonthPurchasesCount: purchasesThisMonth?.Count ?? 0,
                MonthPurchasesTotalEgp: purchasesThisMonth?.Total ?? 0m,
                MonthVatInputEgp: purchasesThisMonth?.Vat ?? 0m);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to build AI context snapshot — chat will continue without it.");
            return AiContextSnapshot.Empty;
        }
    }
}

/// <summary>v5 — point-in-time aggregate snapshot of the tenant's
/// books for the AI chat system prompt. All amounts in EGP.</summary>
public sealed record AiContextSnapshot(
    DateTime CapturedAtUtc,
    int DraftSalesCount,
    int DraftPurchasesCount,
    int MonthSalesCount,
    decimal MonthSalesTotalEgp,
    decimal MonthVatOutputEgp,
    int MonthPurchasesCount,
    decimal MonthPurchasesTotalEgp,
    decimal MonthVatInputEgp)
{
    public static AiContextSnapshot Empty { get; } = new(
        CapturedAtUtc: DateTime.UnixEpoch,
        DraftSalesCount: 0, DraftPurchasesCount: 0,
        MonthSalesCount: 0, MonthSalesTotalEgp: 0m, MonthVatOutputEgp: 0m,
        MonthPurchasesCount: 0, MonthPurchasesTotalEgp: 0m, MonthVatInputEgp: 0m);

    public decimal MonthVatNetEgp => MonthVatOutputEgp - MonthVatInputEgp;
}
