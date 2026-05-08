using System.Text.Json.Serialization;

namespace EgyptTax.Application.Inspection;

/// <summary>
/// US9 / FR-048 — `MANIFEST.sha256` shape per
/// <c>contracts/inspection-bundle-manifest.schema.json</c>. Lists
/// every file in the bundle with its SHA-256 + size, plus the
/// audit-chain extract reference and a top-level archive hash so
/// the verifier script can replay integrity on a clean machine.
///
/// Field names use camelCase + null-omission to match the JSON
/// schema's lowerCamelCase property names; serialization options
/// applied at the writer site so this record stays a plain DTO.
/// </summary>
public sealed record InspectionBundleManifest(
    string BundleVersion,
    BundleCompany Company,
    BundlePeriod Period,
    DateTime GeneratedAt,
    Guid GeneratedByUserId,
    IReadOnlyList<BundleFile> Files,
    BundleAuditChainExtract AuditChainExtract,
    string TopLevelArchiveSha256,
    bool DraftsExcluded,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<Guid>? ExcludedDraftIds = null);

public sealed record BundleCompany(
    Guid CompanyId,
    string Tin,
    BundleBilingualName LegalName);

public sealed record BundleBilingualName(string Ar, string En);

public sealed record BundlePeriod(
    int FiscalYear,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Quarter,
    string Start,
    string End);

public sealed record BundleFile(
    string RelativePath,
    string Sha256,
    long SizeBytes,
    string Category,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? LinkedDocumentId = null);

public sealed record BundleAuditChainExtract(
    long StartIndex,
    long EndIndex,
    string ExtractSha256,
    bool VerifiedAtGeneration);
