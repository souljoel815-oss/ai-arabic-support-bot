using EgyptTax.Application.FixedAssets;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.FixedAssets;

/// <summary>
/// FR-017 / FR-018 / T187 — EF-backed read surface for the fixed-
/// asset pages. List pulls every asset + counts attachments per
/// row (one query for assets + one aggregated query for
/// attachments — N+1 avoided). Per-row NBV is computed in-memory
/// against the engine since the schedule math is non-SQL.
/// </summary>
public sealed class SqlFixedAssetQuery : IFixedAssetQuery
{
    private readonly AppDbContext _db;

    public SqlFixedAssetQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FixedAssetListRow>> ListAsync(
        DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var assets = await _db.Set<FixedAsset>().AsNoTracking()
            .OrderBy(a => a.InServiceDate).ThenBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var assetIds = assets.Select(a => a.Id).ToArray();
        var attachmentCounts = await _db.Set<Attachment>().AsNoTracking()
            .Where(at => at.DocumentType == DocumentType.FixedAsset
                && assetIds.Contains(at.DocumentId))
            .GroupBy(at => at.DocumentId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Count, cancellationToken);

        return assets.Select(a =>
        {
            var nbv = DepreciationEngine.NetBookValueAt(a, asOf);
            var depToDate = MoneyEgp.From(a.Cost.Amount - nbv.Amount);
            return new FixedAssetListRow(
                Id: a.Id,
                Code: a.Code,
                Description: a.Description,
                AssetCategory: a.AssetCategory,
                Status: a.Status,
                Cost: a.Cost,
                InServiceDate: a.InServiceDate,
                UsefulLifeMonths: a.UsefulLifeMonths,
                Convention: a.Convention,
                NetBookValue: nbv,
                DepreciatedToDate: depToDate,
                AttachmentCount: attachmentCounts.GetValueOrDefault(a.Id, 0));
        }).ToList();
    }

    public async Task<FixedAsset?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Set<FixedAsset>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
}
