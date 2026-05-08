using EgyptTax.Application.Compliance.TinRevalidation;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Compliance;

/// <summary>
/// US2 / R-13 — EF-backed implementation of
/// <see cref="ISupplierTinSource"/>. Replaces the US1-time
/// <c>EmptySupplierTinSource</c> stub now that the
/// <see cref="Supplier"/> aggregate ships.
///
/// Selection rules:
///  * Active suppliers only — Inactive rows are not in current use.
///  * <see cref="SupplierTaxProfileType.RegisteredTaxpayer"/> only —
///    Unregistered + ForeignSupplier carry no Egyptian TIN to
///    revalidate.
///  * <see cref="Supplier.LastTinRevalidatedAtUtc"/> is null OR older
///    than the configured <see cref="StaleAfter"/> window. The job's
///    daily cron + 7-day staleness window means each registered
///    supplier is checked roughly once a week — well under the
///    SC-012 dashboard latency budget on a stripped supplier base.
/// </summary>
public sealed class SqlSupplierTinSource : ISupplierTinSource
{
    /// <summary>
    /// Default revalidation freshness window. Suppliers checked more
    /// recently than this are skipped on the next tick.
    /// </summary>
    public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromDays(7);

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly TimeSpan _staleAfter;

    public SqlSupplierTinSource(AppDbContext db, IClock clock)
        : this(db, clock, DefaultStaleAfter) { }

    public SqlSupplierTinSource(AppDbContext db, IClock clock, TimeSpan staleAfter)
    {
        _db = db;
        _clock = clock;
        _staleAfter = staleAfter;
    }

    public async Task<IReadOnlyList<SupplierTinRow>> GetSuppliersToRevalidateAsync(
        CancellationToken cancellationToken = default
    )
    {
        var threshold = _clock.UtcNow - _staleAfter;

        var rows = await _db.Set<Supplier>()
            .AsNoTracking()
            .Where(s =>
                s.Status == SupplierStatus.Active
                && s.TaxProfile.ProfileType == SupplierTaxProfileType.RegisteredTaxpayer
                && s.TaxProfile.TinValue != null
                && (s.LastTinRevalidatedAtUtc == null || s.LastTinRevalidatedAtUtc < threshold)
            )
            .OrderBy(s => s.LastTinRevalidatedAtUtc) // oldest first; nulls float up under SQL Server NULLS-FIRST default
            .Select(s => new
            {
                s.Id,
                Tin = s.TaxProfile.TinValue!,
                NameEn = s.Name.English,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SupplierTinRow(r.Id, r.Tin, r.NameEn)).ToList();
    }
}
