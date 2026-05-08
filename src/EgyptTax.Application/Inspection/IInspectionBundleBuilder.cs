namespace EgyptTax.Application.Inspection;

/// <summary>
/// US9 / FR-048 — port for the period-scoped inspector-handoff
/// bundle. Implementation collects every posted document in the
/// window, every attachment, the audit-chain extract for the
/// period, computes per-file SHA-256, and ZIPs the lot.
///
/// US9 acceptance scenario 3: when <c>allowDrafts=false</c> (the
/// default + recommended) the builder rejects if any drafts dated
/// in the period exist; the operator must explicitly accept by
/// setting <c>allowDrafts=true</c>, which writes the excluded draft
/// ids into <see cref="InspectionBundleManifest.ExcludedDraftIds"/>
/// for the inspector's reference.
/// </summary>
public interface IInspectionBundleBuilder
{
    Task<InspectionBundleResult> BuildAsync(
        InspectionBundleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record InspectionBundleRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Guid GeneratedByUserId,
    bool AllowDrafts);

public sealed record InspectionBundleResult(
    byte[] ZipBytes,
    InspectionBundleManifest Manifest,
    string SuggestedFilename);
