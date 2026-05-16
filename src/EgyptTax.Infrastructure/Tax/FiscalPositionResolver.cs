using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Tax;

/// <summary>
/// v5 F.4 (auto-swap completion) — given a (customer, source VAT
/// category) pair, returns the destination VAT category dictated
/// by the customer's fiscal position. Used by SalesInvoiceEdit
/// when an item is picked so the line's default VAT swaps to the
/// destination automatically (e.g. exporters → 0%, free zone →
/// exempt).
///
/// Cached per request — the same invoice creation may resolve N
/// lines and we don't want a DB hit per line.
///
/// Returns:
///   * (sourceVatCategoryId, null) when no fiscal position assigned
///     OR no mapping rule matches (line keeps the source VAT)
///   * (destinationVatCategoryId.Value, mappingDescription) when a
///     mapping rule applies (caller swaps + shows hint)
///   * (Guid.Empty, "exempt") when the mapping says "no VAT line at
///     all" — caller picks a 0%/exempt VAT category as fallback
///     OR omits the VAT line entirely
/// </summary>
public sealed class FiscalPositionResolver
{
    private readonly AppDbContext _db;
    private readonly Dictionary<Guid, FiscalPosition?> _cache = new();

    public FiscalPositionResolver(AppDbContext db) => _db = db;

    public async Task<FiscalPositionResolution> ResolveAsync(
        Guid customerId,
        Guid sourceVatCategoryId,
        CancellationToken ct = default)
    {
        var customer = await _db.Set<Customer>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer?.FiscalPositionId is not Guid positionId)
        {
            return new FiscalPositionResolution(sourceVatCategoryId, null, null, false);
        }

        if (!_cache.TryGetValue(positionId, out var position))
        {
            position = await _db.Set<FiscalPosition>().AsNoTracking()
                .Include(p => p.Mappings)
                .FirstOrDefaultAsync(p => p.Id == positionId && p.IsActive, ct);
            _cache[positionId] = position;
        }

        if (position is null)
        {
            return new FiscalPositionResolution(sourceVatCategoryId, null, null, false);
        }

        var mapping = position.Mappings
            .FirstOrDefault(m => m.SourceVatCategoryId == sourceVatCategoryId);

        if (mapping is null)
        {
            // Customer has a fiscal position but it doesn't override
            // this particular VAT category — pass through the source.
            return new FiscalPositionResolution(sourceVatCategoryId, null, position, false);
        }

        if (mapping.DestinationVatCategoryId is null)
        {
            // Mapped to "exempt" — return source so caller knows we
            // matched a rule but flag IsExempt = true so the UI can
            // hint "exempt per fiscal position".
            return new FiscalPositionResolution(sourceVatCategoryId, mapping, position, true);
        }

        return new FiscalPositionResolution(
            mapping.DestinationVatCategoryId.Value, mapping, position, false);
    }
}

public sealed record FiscalPositionResolution(
    Guid ResolvedVatCategoryId,
    FiscalPositionMapping? AppliedMapping,
    FiscalPosition? AppliedPosition,
    bool IsExempt);
