using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.FixedAssets;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.FixedAssets;

/// <summary>
/// FR-017 / T187 — creates a Draft FixedAsset. Refuses duplicate
/// codes (the unique-index on the column would also catch it, but
/// failing here surfaces a friendlier error). Emits a
/// <c>fixed_asset.created</c> audit event so the FR-028 chain
/// records the cost basis at creation time — important if the
/// operator later edits the cost on the draft.
/// </summary>
public sealed class CreateFixedAssetHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public CreateFixedAssetHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<FixedAsset> HandleAsync(
        CreateFixedAssetCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _db.Set<FixedAsset>()
            .AsNoTracking()
            .AnyAsync(a => a.Code == command.Code, cancellationToken);
        if (existing)
        {
            throw new InvalidOperationException(
                $"Fixed-asset code '{command.Code}' is already in use; pick a different code."
            );
        }

        var asset = FixedAsset.CreateDraft(
            code: command.Code,
            description: command.Description,
            assetCategory: command.AssetCategory,
            cost: command.Cost,
            inServiceDate: command.InServiceDate,
            usefulLifeMonths: command.UsefulLifeMonths,
            depreciationMethod: command.DepreciationMethod,
            salvageValue: command.SalvageValue,
            convention: command.Convention
        );

        _db.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        var inv = CultureInfo.InvariantCulture;
        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "fixed_asset.created",
                ActorUserId: command.CreatedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"fixed_asset_id":"{{asset.Id:D}}","code":"{{asset.Code}}","cost_egp":{{asset.Cost.Amount.ToString("F2", inv)}},"in_service_date":"{{asset.InServiceDate:yyyy-MM-dd}}","useful_life_months":{{asset.UsefulLifeMonths}},"convention":"{{asset.Convention}}","salvage_egp":{{asset.SalvageValue.Amount.ToString("F2", inv)}}}"""
            ),
            cancellationToken
        );

        return asset;
    }
}
