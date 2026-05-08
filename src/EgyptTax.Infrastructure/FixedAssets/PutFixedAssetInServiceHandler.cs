using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.FixedAssets;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.FixedAssets;

/// <summary>
/// T182 / FR-016 mirror / US6 scenario 3 — fixed-asset Draft →
/// InService transition. Refuses the transition unless at least
/// one Attachment row exists for the asset (capital expenditures
/// MUST have supporting documentation; an inspector can't validate
/// the cost basis without it). The check fires BEFORE
/// <see cref="FixedAsset.PutInService"/> so a rejected attempt
/// leaves the aggregate in Draft + emits an auditable rejection
/// event for the FR-028 chain.
/// </summary>
public sealed class PutFixedAssetInServiceHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public PutFixedAssetInServiceHandler(
        AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<FixedAsset> HandleAsync(
        PutFixedAssetInServiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var asset = await _db.Set<FixedAsset>()
            .FirstOrDefaultAsync(a => a.Id == command.FixedAssetId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Fixed asset {command.FixedAssetId} not found.");

        // FR-016 mirror — capital expenditures require at least one
        // attachment on file before they can be put in service. The
        // operator can attach in any order during draft editing; the
        // gate fires only at the InService transition.
        var attachmentCount = await _db.Set<Attachment>()
            .CountAsync(a => a.DocumentId == asset.Id
                && a.DocumentType == DocumentType.FixedAsset, cancellationToken);
        if (attachmentCount == 0)
        {
            await _auditLog.AppendAsync(new AuditLogPayload(
                Kind: "fixed_asset.put_in_service.rejected_missing_attachment",
                ActorUserId: command.PutInServiceByUserId,
                ActorFirmName: null, CompanyId: Guid.Empty,
                PayloadJson: $$"""{"fixed_asset_id":"{{asset.Id:D}}","code":"{{asset.Code}}"}"""),
                cancellationToken);

            throw new InvalidOperationException(
                $"Cannot put fixed asset {asset.Id} ({asset.Code}) in service: at least one attachment is required (FR-016 mirror). " +
                "Attach the supporting documentation (purchase receipt / installation certificate / etc.) and retry.");
        }

        var nowUtc = _clock.UtcNow;
        asset.PutInService();
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(new AuditLogPayload(
            Kind: "fixed_asset.put_in_service",
            ActorUserId: command.PutInServiceByUserId,
            ActorFirmName: null, CompanyId: Guid.Empty,
            PayloadJson: BuildPayloadJson(asset, nowUtc)),
            cancellationToken);

        return asset;
    }

    private static string BuildPayloadJson(FixedAsset a, DateTime nowUtc)
    {
        var inv = CultureInfo.InvariantCulture;
        return $$"""{"fixed_asset_id":"{{a.Id:D}}","code":"{{a.Code}}","cost_egp":{{a.Cost.Amount.ToString("F2", inv)}},"in_service_date":"{{a.InServiceDate:yyyy-MM-dd}}","useful_life_months":{{a.UsefulLifeMonths}},"convention":"{{a.Convention}}","put_in_service_at_utc":"{{nowUtc.ToString("o", inv)}}"}""";
    }
}
