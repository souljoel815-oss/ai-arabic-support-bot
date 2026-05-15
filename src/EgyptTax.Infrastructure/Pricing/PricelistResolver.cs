using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Pricing;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Pricing;

/// <summary>
/// v5 B.2 — resolves the unit price for an (item, customer) pair
/// against the customer's default <see cref="Pricelist"/>. Used by
/// SalesInvoiceEdit when the operator picks an item on a new line
/// so the unit price pre-fills correctly. Falls back to
/// <see cref="Item.UnitPrice"/> when no pricelist is assigned or
/// no rule matches.
///
/// Caches per request — the same invoice creation may resolve
/// 10+ lines and we don't want a DB hit per line.
/// </summary>
public sealed class PricelistResolver
{
    private readonly AppDbContext _db;
    private readonly Dictionary<Guid, Pricelist?> _pricelistCache = new();

    public PricelistResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PriceResolution> ResolveAsync(
        Guid customerId,
        Guid itemId,
        decimal listPriceEgp,
        CancellationToken ct = default)
    {
        var customer = await _db.Set<Customer>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer?.DefaultPricelistId is not Guid pricelistId)
        {
            return new PriceResolution(listPriceEgp, AppliedRule: null, AppliedPricelistName: null);
        }

        if (!_pricelistCache.TryGetValue(pricelistId, out var pricelist))
        {
            pricelist = await _db.Set<Pricelist>().AsNoTracking()
                .Include(p => p.Rules)
                .FirstOrDefaultAsync(p => p.Id == pricelistId
                    && p.Status == PricelistStatus.Active, ct);
            _pricelistCache[pricelistId] = pricelist;
        }

        if (pricelist is null)
        {
            return new PriceResolution(listPriceEgp, AppliedRule: null, AppliedPricelistName: null);
        }

        var matchingRule = pricelist.Rules
            .OrderBy(r => r.Sequence)
            .FirstOrDefault(r => r.Matches(itemId, customerId));

        if (matchingRule is null)
        {
            return new PriceResolution(listPriceEgp, AppliedRule: null,
                AppliedPricelistName: pricelist.Name);
        }

        var resolved = matchingRule.Apply(listPriceEgp);
        return new PriceResolution(resolved, matchingRule, pricelist.Name);
    }
}

public sealed record PriceResolution(
    decimal UnitPriceEgp,
    PricelistRule? AppliedRule,
    ArabicEnglishText? AppliedPricelistName);
