using EgyptTax.Domain.Inventory;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Inventory;

/// <summary>
/// v5 F.5 — pure compute helper. Given a Draft landed-cost +
/// chosen target purchase-invoice line ids, produces the per-line
/// allocation amounts according to the aggregate's
/// <see cref="LandedCostSplitMethod"/>.
///
/// Pure CPU after the DB lookup — no side effects. Caller stuffs
/// the result into <see cref="LandedCost.SetAllocations"/> and
/// SaveChanges separately. Keeps the aggregate ignorant of the DB.
/// </summary>
public sealed class LandedCostComputeService
{
    private readonly AppDbContext _db;

    public LandedCostComputeService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<(Guid PurchaseInvoiceLineId, MoneyEgp Amount)>> ComputeAsync(
        LandedCost landedCost,
        IReadOnlyCollection<Guid> targetPurchaseInvoiceLineIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(landedCost);
        if (targetPurchaseInvoiceLineIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Pick at least one purchase-invoice line to allocate against.");
        }

        var totalCost = landedCost.TotalCost().Amount;
        if (totalCost <= 0m)
        {
            throw new InvalidOperationException(
                "Cannot compute allocations: cost-line total is zero. Add at least one cost line first.");
        }

        var ids = targetPurchaseInvoiceLineIds.ToList();
        var lines = await _db.Set<PurchaseInvoiceLine>().AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.ItemId, l.Quantity, UnitPrice = l.UnitPrice.Amount })
            .ToListAsync(ct);

        if (lines.Count != ids.Count)
        {
            var missing = ids.Except(lines.Select(l => l.Id)).ToList();
            throw new InvalidOperationException(
                $"Purchase-invoice line(s) not found: {string.Join(", ", missing)}");
        }

        // v5 F.5 v2 — when the split method needs item physical attrs,
        // pull WeightKg / VolumeM3 for the items referenced by the lines.
        // Lines whose item is null (expense-category PI lines) or whose
        // attribute is null get zero weight (skipped from allocation).
        Dictionary<Guid, EgyptTax.Domain.MasterData.Item>? itemAttrs = null;
        if (landedCost.SplitMethod == LandedCostSplitMethod.ByWeight
            || landedCost.SplitMethod == LandedCostSplitMethod.ByVolume)
        {
            var itemIds = lines.Where(l => l.ItemId.HasValue)
                .Select(l => l.ItemId!.Value).Distinct().ToList();
            itemAttrs = await _db.Set<EgyptTax.Domain.MasterData.Item>().AsNoTracking()
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, ct);
        }

        decimal LineWeight(decimal qty, Guid? itemId) =>
            itemId is { } id && itemAttrs!.TryGetValue(id, out var item) && item.WeightKg is { } w ? qty * w : 0m;
        decimal LineVolume(decimal qty, Guid? itemId) =>
            itemId is { } id && itemAttrs!.TryGetValue(id, out var item) && item.VolumeM3 is { } v ? qty * v : 0m;

        var weights = landedCost.SplitMethod switch
        {
            LandedCostSplitMethod.Equal => lines.Select(l => (l.Id, Weight: 1m)).ToList(),
            LandedCostSplitMethod.ByQty => lines.Select(l => (l.Id, Weight: l.Quantity)).ToList(),
            LandedCostSplitMethod.ByCost => lines.Select(l => (l.Id, Weight: l.Quantity * l.UnitPrice)).ToList(),
            LandedCostSplitMethod.ByWeight => lines.Select(l => (l.Id, Weight: LineWeight(l.Quantity, l.ItemId))).ToList(),
            LandedCostSplitMethod.ByVolume => lines.Select(l => (l.Id, Weight: LineVolume(l.Quantity, l.ItemId))).ToList(),
            _ => throw new InvalidOperationException($"Unsupported split method {landedCost.SplitMethod}."),
        };

        var totalWeight = weights.Sum(w => w.Weight);
        if (totalWeight <= 0m)
        {
            var hint = landedCost.SplitMethod switch
            {
                LandedCostSplitMethod.ByWeight => " None of the target items has Item.WeightKg set — fill it on /items/{id}/edit.",
                LandedCostSplitMethod.ByVolume => " None of the target items has Item.VolumeM3 set — fill it on /items/{id}/edit.",
                _ => "",
            };
            throw new InvalidOperationException(
                $"Split method {landedCost.SplitMethod} produced a zero total weight (every target line has zero qty/cost/attr).{hint}");
        }

        // Allocate proportionally with rounding-residual to the largest
        // line so the sum of allocations exactly equals the cost total.
        var rawAlloc = weights
            .Select(w => (w.Id, Amount: decimal.Round(totalCost * w.Weight / totalWeight, 2, MidpointRounding.AwayFromZero)))
            .ToList();
        var residual = totalCost - rawAlloc.Sum(a => a.Amount);
        if (residual != 0m && rawAlloc.Count > 0)
        {
            var largestIdx = 0;
            for (int i = 1; i < rawAlloc.Count; i++)
            {
                if (rawAlloc[i].Amount > rawAlloc[largestIdx].Amount) largestIdx = i;
            }
            var (id, amt) = rawAlloc[largestIdx];
            rawAlloc[largestIdx] = (id, amt + residual);
        }

        return rawAlloc
            .Select(a => (a.Id, MoneyEgp.From(a.Amount)))
            .ToList();
    }
}
