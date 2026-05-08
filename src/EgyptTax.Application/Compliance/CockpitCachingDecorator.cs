using Microsoft.Extensions.Caching.Memory;

namespace EgyptTax.Application.Compliance;

/// <summary>
/// T236a / Round-6 F13 — caching decorator over
/// <see cref="IMonthlyTaxClosingCockpitQuery"/>. The cockpit page is
/// "did I miss anything?" — operators refresh it repeatedly during
/// month-end closing and the underlying query joins ~10 different
/// tables (VAT, ETA, period locks, attachments, risk rules). 30-second
/// sliding expiration means a streak of refreshes within half a minute
/// hits the cache; longer gaps re-query so freshly-posted docs show up
/// without manual invalidation.
///
/// On any tax-impacting state change (sales-invoice / purchase-invoice
/// / expense / journal-voucher post; period lock / reopen) the
/// post-handler should call
/// <see cref="ICockpitCacheInvalidator.InvalidateForMonth(int, int)"/>
/// for the affected month so the next refresh sees the new state
/// immediately rather than waiting for the sliding expiration.
///
/// Cache key shape: <c>"cockpit:{year}-{month:D2}"</c>. Single-tenant
/// MVP — when multi-tenant lands, the company_id is prefixed.
/// </summary>
public sealed class CockpitCachingDecorator
    : IMonthlyTaxClosingCockpitQuery,
        ICockpitCacheInvalidator
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromSeconds(30);

    private readonly IMonthlyTaxClosingCockpitQuery _inner;
    private readonly IMemoryCache _cache;

    public CockpitCachingDecorator(IMonthlyTaxClosingCockpitQuery inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<MonthlyTaxClosingCockpit> RunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default
    )
    {
        var key = BuildKey(year, month);
        if (_cache.TryGetValue(key, out MonthlyTaxClosingCockpit? cached) && cached is not null)
        {
            return cached;
        }

        var result = await _inner.RunAsync(year, month, cancellationToken);
        _cache.Set(
            key,
            result,
            new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration }
        );
        return result;
    }

    public void InvalidateForMonth(int year, int month) => _cache.Remove(BuildKey(year, month));

    public void InvalidateAll()
    {
        // IMemoryCache has no clear-all; the decorator only owns
        // its own keys. Iterating every (year, month) pair is
        // wasteful — instead, callers that need a full clear can
        // re-construct the cache. For the MVP we just no-op + rely
        // on sliding expiration to drain entries within 30 s.
    }

    private static string BuildKey(int year, int month) => $"cockpit:{year}-{month:D2}";
}

/// <summary>
/// T236a / Round-6 F13 — busts the
/// <see cref="CockpitCachingDecorator"/> entry for a given month so a
/// freshly-posted document shows up on the next cockpit refresh
/// without waiting for the 30-second sliding expiration. Wired into
/// every post-handler (sales / purchase / expense / journal voucher)
/// + period lock/reopen handlers.
/// </summary>
public interface ICockpitCacheInvalidator
{
    void InvalidateForMonth(int year, int month);
    void InvalidateAll();
}
