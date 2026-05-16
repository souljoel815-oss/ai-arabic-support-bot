using System.Linq;
using EgyptTax.Domain.Inventory;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Inventory;

/// <summary>
/// v5 A.2 / E.4 — weighted-average unit cost per item, computed
/// from posted purchase-invoice lines. Same calc the Stock
/// Valuation report uses (extracted to a service so the
/// StockAdjustments page can emit JVs valued at avg cost without
/// duplicating the logic).
///
/// v5 F.5 — when a <see cref="LandedCost"/> aggregate has been
/// validated and its allocations target the same purchase-invoice
/// lines, those landed-cost amounts roll into the per-item cost
/// total: total_spend = sum(qty × unit_price) + sum(landed_cost_alloc).
///
/// Items with no posted purchases return null — the caller decides
/// whether to skip the JV emit (E.4 path) or render "—" in a
/// report cell (A.2 path).
/// </summary>
public sealed class WeightedAvgCostQuery
{
    private readonly AppDbContext _db;

    public WeightedAvgCostQuery(AppDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, decimal?>> GetCostByItemAsync(
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken ct = default)
    {
        if (itemIds.Count == 0) return new Dictionary<Guid, decimal?>();
        var idList = itemIds.ToList();

        // PurchaseInvoiceLine.ItemId is nullable (the line can also
        // be an expense-category bill line), so we filter to non-null
        // matching item-ids client-side after pulling.
        var rows = await _db.Set<PurchaseInvoiceLine>().AsNoTracking()
            .Where(l => l.ItemId != null
                && _db.Set<PurchaseInvoice>()
                    .Any(pi => pi.Id == l.PurchaseInvoiceId
                        && pi.State == DocumentState.Posted))
            .Select(l => new { LineId = l.Id, ItemId = l.ItemId!.Value, l.Quantity, UnitPrice = l.UnitPrice.Amount })
            .ToListAsync(ct);

        var matchingRows = rows.Where(r => idList.Contains(r.ItemId)).ToList();
        var matchingLineIds = matchingRows.Select(r => r.LineId).ToList();

        // v5 F.5 — landed-cost allocations only count once their
        // parent LandedCost is Validated. Gathered per purchase-line.
        var landedByLine = matchingLineIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : (await _db.Set<LandedCostAllocation>().AsNoTracking()
                .Where(a => matchingLineIds.Contains(a.PurchaseInvoiceLineId)
                    && _db.Set<LandedCost>()
                        .Any(lc => lc.Id == a.LandedCostId
                            && lc.State == LandedCostState.Validated))
                .Select(a => new { a.PurchaseInvoiceLineId, Amount = a.AllocatedAmount.Amount })
                .ToListAsync(ct))
            .GroupBy(x => x.PurchaseInvoiceLineId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return matchingRows
            .GroupBy(x => x.ItemId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var totalQty = g.Sum(x => x.Quantity);
                    if (totalQty <= 0m) return (decimal?)null;
                    var goodsSpend = g.Sum(x => x.Quantity * x.UnitPrice);
                    var landed = g.Sum(x => landedByLine.TryGetValue(x.LineId, out var lc) ? lc : 0m);
                    return (decimal?)decimal.Round((goodsSpend + landed) / totalQty, 4, MidpointRounding.ToEven);
                });
    }

    public async Task<decimal?> GetCostForItemAsync(Guid itemId, CancellationToken ct = default)
    {
        var dict = await GetCostByItemAsync(new[] { itemId }, ct);
        return dict.TryGetValue(itemId, out var c) ? c : null;
    }
}
