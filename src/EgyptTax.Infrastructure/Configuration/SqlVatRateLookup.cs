using EgyptTax.Application.Configuration;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Configuration;

/// <summary>
/// FR-019 / FR-022 / US5 — EF-backed VAT-rate lookup. Selects the
/// row whose (code, effective window) covers <paramref name="date"/>;
/// ties broken by the latest <c>EffectiveFromDate</c> so the
/// "supersede by inserting newer" model works without UPDATE.
/// </summary>
public sealed class SqlVatRateLookup : IVatRateLookup
{
    private readonly AppDbContext _db;

    public SqlVatRateLookup(AppDbContext db)
    {
        _db = db;
    }

    public Task<VatCategory?> GetEffectiveAsync(
        string code, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return _db.Set<VatCategory>().AsNoTracking()
            .Where(c => c.Code == code
                && c.EffectiveFromDate <= date
                && (c.EffectiveToDate == null || c.EffectiveToDate >= date))
            .OrderByDescending(c => c.EffectiveFromDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VatCategory>> ListByCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return await _db.Set<VatCategory>().AsNoTracking()
            .Where(c => c.Code == code)
            .OrderBy(c => c.EffectiveFromDate)
            .ToListAsync(cancellationToken);
    }
}
