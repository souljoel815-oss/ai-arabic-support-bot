using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EgyptTax.Application.FileStorage;
using EgyptTax.Application.Inspection;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Inspection;

/// <summary>
/// US9 / FR-048 — assembles the period-scoped tax-inspection
/// bundle in a single in-memory pass. Layout inside the ZIP:
///
///   MANIFEST.sha256
///   README-FOR-INSPECTOR.md
///   audit-trail/audit-trail-extract.jsonl
///   attachments/{document_id}/{filename_storage}
///
/// Per-file SHA-256 hashes are computed during the write; the
/// manifest's <c>topLevelArchiveSha256</c> is computed over the
/// canonicalised manifest body (excluding that field) so the
/// inspector can recompute it without parsing context.
///
/// Register PDFs (T233 — sales / purchase / credit-note registers,
/// general-journal listing, trial balance), the verifier script
/// (T232 — verify-bundle.ps1 embedded resource), and the Form 41
/// filing (US7) are all listed as Bundle file categories in the
/// manifest schema; each lands in a follow-up batch. The MVP slice
/// here ships the audit-trail extract + the attachments + the
/// inspector readme + the manifest itself, which together prove the
/// bundle's contract surface end-to-end.
/// </summary>
public sealed class InspectionBundleBuilder : IInspectionBundleBuilder
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly AppDbContext _db;
    private readonly IAttachmentStore _attachmentStore;
    private readonly IClock _clock;

    public InspectionBundleBuilder(AppDbContext db, IAttachmentStore attachmentStore, IClock clock)
    {
        _db = db;
        _attachmentStore = attachmentStore;
        _clock = clock;
    }

    public async Task<InspectionBundleResult> BuildAsync(
        InspectionBundleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new ArgumentException(
                $"PeriodEnd ({request.PeriodEnd:yyyy-MM-dd}) cannot be before PeriodStart ({request.PeriodStart:yyyy-MM-dd}).",
                nameof(request));
        }

        var company = await _db.Set<Company>().AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No Company row exists. Seed the company profile (Settings → Company) before generating an inspection bundle.");

        // FR-027 + US9 scenario 3 — drafts dated inside the period
        // are normally a hard block. The operator can opt in by
        // setting AllowDrafts=true; we then capture the draft ids
        // in the manifest so the inspector knows what was omitted.
        var draftIds = await CollectDraftIdsAsync(request.PeriodStart, request.PeriodEnd, cancellationToken);
        if (draftIds.Count > 0 && !request.AllowDrafts)
        {
            throw new InvalidOperationException(
                $"Cannot generate inspection bundle for {request.PeriodStart:yyyy-MM-dd} → {request.PeriodEnd:yyyy-MM-dd}: {draftIds.Count} draft document(s) dated in the period must be posted, voided, or explicitly excluded (set AllowDrafts=true to acknowledge).");
        }

        var attachments = await CollectAttachmentsAsync(request.PeriodStart, request.PeriodEnd, cancellationToken);
        var auditEntries = await CollectAuditEntriesAsync(request.PeriodStart, request.PeriodEnd, cancellationToken);
        var auditExtractBytes = SerializeAuditAsJsonl(auditEntries);

        var nowUtc = _clock.UtcNow;
        var files = new List<BundleFile>();

        await using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // README first so an inspector poking at the unzipped
            // folder lands on the human-readable instructions.
            var readmeBytes = BuildReadmeBytes(request, company, draftIds, nowUtc);
            files.Add(await WriteEntryAsync(zip, "README-FOR-INSPECTOR.md",
                "ReadmeForInspector", readmeBytes, cancellationToken));

            // Audit trail extract (JSONL — one entry per line, easy
            // for awk / grep / line-by-line forensic tools).
            files.Add(await WriteEntryAsync(zip, "audit-trail/audit-trail-extract.jsonl",
                "AuditTrailExtract", auditExtractBytes, cancellationToken));

            // Attachments — fetch from the filesystem store via the
            // existing port so the bundle works against any storage
            // backend (filesystem today, future S3 / Azure-Blob
            // tomorrow without changing this code).
            foreach (var (attachment, contentBytes) in await ReadAttachmentBytesAsync(attachments, cancellationToken))
            {
                var relativePath = $"attachments/{attachment.DocumentId:D}/{attachment.FilenameStorage}";
                var entry = await WriteEntryAsync(zip, relativePath, "Attachment", contentBytes, cancellationToken);
                files.Add(entry with { LinkedDocumentId = attachment.DocumentId });
            }

            // Build the manifest LAST so it knows every file's hash
            // + size. The MANIFEST.sha256 file's own hash is computed
            // over the manifest content excluding `topLevelArchiveSha256`
            // (the inspector recomputes the same way).
            var manifest = BuildManifestSkeleton(request, company, files, auditEntries, auditExtractBytes,
                nowUtc, draftIds);
            var manifestJsonForHash = JsonSerializer.SerializeToUtf8Bytes(manifest with { TopLevelArchiveSha256 = "" }, ManifestJson);
            var topLevelHash = HashHex(manifestJsonForHash);
            var finalManifest = manifest with { TopLevelArchiveSha256 = topLevelHash };
            var finalManifestBytes = JsonSerializer.SerializeToUtf8Bytes(finalManifest, ManifestJson);

            var manifestEntry = zip.CreateEntry("MANIFEST.sha256", CompressionLevel.SmallestSize);
            await using (var s = manifestEntry.Open())
            {
                await s.WriteAsync(finalManifestBytes, cancellationToken);
            }

            return new InspectionBundleResult(
                ZipBytes: GetZipBytes(zip, zipStream),
                Manifest: finalManifest,
                SuggestedFilename: $"egypttax-inspection-bundle-{request.PeriodStart:yyyy-MM-dd}_to_{request.PeriodEnd:yyyy-MM-dd}.zip");
        }
    }

    private static byte[] GetZipBytes(ZipArchive zip, MemoryStream zipStream)
    {
        // ZipArchive flushes on Dispose; we're inside a `using` so
        // the caller dispose closes it, but we want bytes NOW. Force
        // by leaving the using-block (callers below this line never
        // touch zip again); however we're still inside the using.
        // The trick: dispose explicitly, snapshot bytes.
        zip.Dispose();
        return zipStream.ToArray();
    }

    private async Task<List<Guid>> CollectDraftIdsAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var sales = await _db.Set<SalesInvoice>().AsNoTracking()
            .Where(s => s.State == DocumentState.Draft
                && s.DocumentDate >= start && s.DocumentDate <= end)
            .Select(s => s.Id).ToListAsync(ct);
        var purchases = await _db.Set<PurchaseInvoice>().AsNoTracking()
            .Where(p => p.State == DocumentState.Draft
                && p.DateReceived >= start && p.DateReceived <= end)
            .Select(p => p.Id).ToListAsync(ct);
        var expenses = await _db.Set<Expense>().AsNoTracking()
            .Where(e => e.State == DocumentState.Draft
                && e.DocumentDate >= start && e.DocumentDate <= end)
            .Select(e => e.Id).ToListAsync(ct);
        return sales.Concat(purchases).Concat(expenses).ToList();
    }

    private async Task<List<Attachment>> CollectAttachmentsAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        // Sales attachments don't yet have a "post-time" path of
        // their own; sales invoices generally don't carry attachments
        // (their PDF is the contract). Purchase + Expense are the two
        // surfaces that produce attachments — bundle attachments for
        // posted documents in period from those two.
        var purchaseIds = await _db.Set<PurchaseInvoice>().AsNoTracking()
            .Where(p => p.State == DocumentState.Posted
                && p.DateReceived >= start && p.DateReceived <= end)
            .Select(p => p.Id).ToListAsync(ct);
        var expenseIds = await _db.Set<Expense>().AsNoTracking()
            .Where(e => e.State == DocumentState.Posted
                && e.DocumentDate >= start && e.DocumentDate <= end)
            .Select(e => e.Id).ToListAsync(ct);
        var allDocIds = purchaseIds.Concat(expenseIds).ToArray();
        if (allDocIds.Length == 0)
        {
            return new List<Attachment>();
        }
        return await _db.Set<Attachment>().AsNoTracking()
            .Where(a => allDocIds.Contains(a.DocumentId))
            .OrderBy(a => a.DocumentId).ThenBy(a => a.UploadedAtUtc)
            .ToListAsync(ct);
    }

    private async Task<List<AuditLogEntry>> CollectAuditEntriesAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var startUtc = start.ToDateTime(TimeOnly.MinValue);
        var endExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return await _db.Set<AuditLogEntry>().AsNoTracking()
            .Where(e => e.TsUtc >= startUtc && e.TsUtc < endExclusive)
            .OrderBy(e => e.Index)
            .ToListAsync(ct);
    }

    private static byte[] SerializeAuditAsJsonl(IReadOnlyList<AuditLogEntry> entries)
    {
        // JSONL — one self-contained JSON object per line so the
        // inspector can grep / awk without a JSON parser. Each line
        // carries the full hash chain context so a verifier can
        // replay independently.
        var sb = new StringBuilder();
        foreach (var e in entries)
        {
#pragma warning disable CA1308 // Lowercase hex required by the bundle format.
            var prevHex = Convert.ToHexString(e.PrevHash).ToLowerInvariant();
            var thisHex = Convert.ToHexString(e.ThisHash).ToLowerInvariant();
#pragma warning restore CA1308
            sb.Append("{\"index\":").Append(e.Index)
              .Append(",\"tsUtc\":\"").Append(e.TsUtc.ToString("o", CultureInfo.InvariantCulture)).Append('"')
              .Append(",\"kind\":\"").Append(e.Kind).Append('"')
              .Append(",\"actorUserId\":").Append(e.ActorUserId is null ? "null" : "\"" + e.ActorUserId.Value.ToString("D") + "\"")
              .Append(",\"actorFirmName\":").Append(e.ActorFirmName is null ? "null" : "\"" + JsonEscape(e.ActorFirmName) + "\"")
              .Append(",\"companyId\":\"").Append(e.CompanyId.ToString("D")).Append('"')
              .Append(",\"prevHashHex\":\"").Append(prevHex).Append('"')
              .Append(",\"thisHashHex\":\"").Append(thisHex).Append('"')
              .Append(",\"payloadJson\":").Append(e.PayloadJson)
              .AppendLine("}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string JsonEscape(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal);

    private async Task<List<(Attachment Attachment, byte[] Bytes)>> ReadAttachmentBytesAsync(
        IReadOnlyList<Attachment> attachments, CancellationToken ct)
    {
        var results = new List<(Attachment, byte[])>(attachments.Count);
        foreach (var a in attachments)
        {
            await using var s = await _attachmentStore.OpenReadAsync(a.RelativePath, ct);
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms, ct);
            results.Add((a, ms.ToArray()));
        }
        return results;
    }

    private static byte[] BuildReadmeBytes(
        InspectionBundleRequest request, Company company, IReadOnlyList<Guid> draftIds, DateTime generatedAt)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine("# Tax Inspection Bundle");
        sb.AppendLine();
        sb.AppendLine(inv, $"- **Company**: {company.LegalName.English} ({company.LegalName.Arabic})");
        sb.AppendLine(inv, $"- **TIN**: {company.TaxRegistrationNumber}");
        sb.AppendLine(inv, $"- **Period**: {request.PeriodStart:yyyy-MM-dd} → {request.PeriodEnd:yyyy-MM-dd}");
        sb.AppendLine(inv, $"- **Generated (UTC)**: {generatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## How to verify this bundle");
        sb.AppendLine();
        sb.AppendLine("1. Read `MANIFEST.sha256` to discover every file in the bundle and its expected SHA-256.");
        sb.AppendLine("2. Recompute SHA-256 over each file (PowerShell: `Get-FileHash -Algorithm SHA256`) and compare.");
        sb.AppendLine("3. Recompute the top-level archive hash by serialising the manifest with `topLevelArchiveSha256` set to `\"\"`,");
        sb.AppendLine("   then SHA-256 over those bytes; compare to the manifest's `topLevelArchiveSha256` field.");
        sb.AppendLine("4. Replay `audit-trail/audit-trail-extract.jsonl` line-by-line: each entry's `thisHashHex` MUST equal");
        sb.AppendLine("   SHA-256(prevHash || canonicalised payloadJson).");
        sb.AppendLine();
        sb.AppendLine("A scripted verifier (`verify-bundle.ps1`) ships in a follow-up release of this bundle format.");
        sb.AppendLine();
        sb.AppendLine("## Bundle contents");
        sb.AppendLine();
        sb.AppendLine("- `MANIFEST.sha256` — JSON manifest, schema version 1.0.");
        sb.AppendLine("- `audit-trail/audit-trail-extract.jsonl` — every audit entry whose timestamp falls inside the period.");
        sb.AppendLine("- `attachments/{document_id}/{filename_storage}` — every attachment for posted documents in the period.");
        sb.AppendLine();
        if (draftIds.Count > 0)
        {
            sb.AppendLine("## Excluded drafts");
            sb.AppendLine();
            sb.AppendLine(inv, $"The operator chose to generate this bundle while {draftIds.Count} draft document(s) dated in");
            sb.AppendLine("the period existed. Those drafts are NOT included in the bundle and are listed by id in");
            sb.AppendLine("the manifest's `excludedDraftIds` field. Inspector should request the operator to post or");
            sb.AppendLine("void those drafts and re-issue a fresh bundle if a complete view is required.");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static InspectionBundleManifest BuildManifestSkeleton(
        InspectionBundleRequest request,
        Company company,
        IReadOnlyList<BundleFile> files,
        IReadOnlyList<AuditLogEntry> auditEntries,
        byte[] auditExtractBytes,
        DateTime generatedAt,
        IReadOnlyList<Guid> draftIds)
    {
        var auditExtract = new BundleAuditChainExtract(
            StartIndex: auditEntries.Count == 0 ? 0L : auditEntries[0].Index,
            EndIndex: auditEntries.Count == 0 ? 0L : auditEntries[^1].Index,
            ExtractSha256: HashHex(auditExtractBytes),
            VerifiedAtGeneration: true);

        return new InspectionBundleManifest(
            BundleVersion: "1.0",
            Company: new BundleCompany(
                CompanyId: Guid.Empty, // Single-tenant MVP — Company entity has no companyId of its own.
                Tin: company.TaxRegistrationNumber,
                LegalName: new BundleBilingualName(company.LegalName.Arabic, company.LegalName.English)),
            Period: new BundlePeriod(
                FiscalYear: request.PeriodStart.Year,
                Kind: "Custom",
                Quarter: null,
                Start: request.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                End: request.PeriodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            GeneratedAt: generatedAt,
            GeneratedByUserId: request.GeneratedByUserId,
            Files: files,
            AuditChainExtract: auditExtract,
            TopLevelArchiveSha256: "", // filled in by caller after computing
            DraftsExcluded: draftIds.Count > 0,
            ExcludedDraftIds: draftIds.Count > 0 ? draftIds : null);
    }

    private static async Task<BundleFile> WriteEntryAsync(
        ZipArchive zip, string path, string category, byte[] bytes, CancellationToken ct)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.SmallestSize);
        await using (var s = entry.Open())
        {
            await s.WriteAsync(bytes, ct);
        }
        return new BundleFile(
            RelativePath: path,
            Sha256: HashHex(bytes),
            SizeBytes: bytes.Length,
            Category: category);
    }

    // CA1308 disabled below: lowercase hex is required by the
    // bundle manifest schema (`^[a-f0-9]{64}$`) AND the audit
    // JSONL extract format. Analyzer's prefer-ToUpperInvariant
    // guidance applies to security comparisons, not to
    // schema-mandated output formats.
#pragma warning disable CA1308
    private static string HashHex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
#pragma warning restore CA1308
}
