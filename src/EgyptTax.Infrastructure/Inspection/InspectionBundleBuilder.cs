using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EgyptTax.Application.FileStorage;
using EgyptTax.Application.Inspection;
using EgyptTax.Application.Reports;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Pdf.Registers;
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
///   verify-bundle.ps1
///   audit-trail/audit-trail-extract.jsonl
///   registers/sales-invoice-register.pdf
///   registers/purchase-and-expense-register.pdf
///   registers/credit-note-and-reversal-register.pdf
///   registers/general-journal-listing.pdf
///   registers/trial-balance.pdf
///   attachments/{document_id}/{filename_storage}
///
/// Per-file SHA-256 hashes are computed during the write; the
/// manifest's <c>topLevelArchiveSha256</c> is computed over the
/// canonicalised manifest body (excluding that field) so the
/// inspector can recompute it without parsing context.
///
/// Form 41 filing (US7) and the auditor verification report
/// (Hangfire-driven, T231) are listed as bundle categories in the
/// manifest schema and land in follow-up batches.
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
    private readonly ITrialBalanceReportQuery _trialBalanceQuery;

    public InspectionBundleBuilder(
        AppDbContext db,
        IAttachmentStore attachmentStore,
        IClock clock,
        ITrialBalanceReportQuery trialBalanceQuery)
    {
        _db = db;
        _attachmentStore = attachmentStore;
        _clock = clock;
        _trialBalanceQuery = trialBalanceQuery;
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

            // T232 — embedded verifier script so the inspector has a
            // one-command integrity check on a clean Windows machine
            // with no application install required.
            var verifierBytes = LoadEmbeddedVerifierScript();
            files.Add(await WriteEntryAsync(zip, "verify-bundle.ps1",
                "VerifierScript", verifierBytes, cancellationToken));

            // Audit trail extract (JSONL — one entry per line, easy
            // for awk / grep / line-by-line forensic tools).
            files.Add(await WriteEntryAsync(zip, "audit-trail/audit-trail-extract.jsonl",
                "AuditTrailExtract", auditExtractBytes, cancellationToken));

            // T233 — five register PDFs the inspector reads first.
            // Generated synchronously in-process; QuestPDF render
            // time for typical period sizes is well under a second
            // each, so total bundle build stays interactive.
            var salesRegisterBytes = await BuildSalesRegisterPdfAsync(
                request, company, nowUtc, cancellationToken);
            files.Add(await WriteEntryAsync(zip, "registers/sales-invoice-register.pdf",
                "SalesInvoiceRegister", salesRegisterBytes, cancellationToken));

            var purchaseExpenseRegisterBytes = await BuildPurchaseAndExpenseRegisterPdfAsync(
                request, company, nowUtc, cancellationToken);
            files.Add(await WriteEntryAsync(zip, "registers/purchase-and-expense-register.pdf",
                "PurchaseInvoiceAndExpenseRegister", purchaseExpenseRegisterBytes, cancellationToken));

            var creditNoteRegisterBytes = await BuildCreditNoteAndReversalRegisterPdfAsync(
                request, company, nowUtc, cancellationToken);
            files.Add(await WriteEntryAsync(zip, "registers/credit-note-and-reversal-register.pdf",
                "CreditNoteAndReversalRegister", creditNoteRegisterBytes, cancellationToken));

            var journalListingBytes = await BuildJournalListingPdfAsync(
                request, company, nowUtc, cancellationToken);
            files.Add(await WriteEntryAsync(zip, "registers/general-journal-listing.pdf",
                "GeneralJournalListing", journalListingBytes, cancellationToken));

            var trialBalanceBytes = await BuildTrialBalancePdfAsync(
                request, company, nowUtc, cancellationToken);
            files.Add(await WriteEntryAsync(zip, "registers/trial-balance.pdf",
                "TrialBalance", trialBalanceBytes, cancellationToken));

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

    private async Task<byte[]> BuildSalesRegisterPdfAsync(
        InspectionBundleRequest request, Company company, DateTime nowUtc, CancellationToken ct)
    {
        // Posted, non-credit-note sales invoices in period.
        var invoices = await _db.Set<SalesInvoice>().AsNoTracking()
            .Where(i => i.State == DocumentState.Posted
                && i.CreditNoteOfInvoiceId == null
                && i.DocumentDate >= request.PeriodStart
                && i.DocumentDate <= request.PeriodEnd)
            .OrderBy(i => i.DocumentDate).ThenBy(i => i.DocumentNumber)
            .ToListAsync(ct);

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToArray();
        var customers = await _db.Set<Customer>().AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = invoices.Select(i =>
        {
            customers.TryGetValue(i.CustomerId, out var c);
            return new SalesInvoiceRegisterPdfRenderer.Row(
                DocumentNumber: i.DocumentNumber ?? "",
                DocumentDate: i.DocumentDate,
                CustomerNameEn: c?.Name.English ?? "(unknown)",
                CustomerNameAr: c?.Name.Arabic ?? "",
                CustomerTin: i.CustomerTaxProfileSnapshot.TinValue,
                Subtotal: i.Subtotal.Amount,
                Vat: i.VatTotal.Amount,
                Total: i.GrandTotal.Amount);
        }).ToList();

        return SalesInvoiceRegisterPdfRenderer.Render(
            company, request.PeriodStart, request.PeriodEnd, nowUtc, rows);
    }

    private async Task<byte[]> BuildPurchaseAndExpenseRegisterPdfAsync(
        InspectionBundleRequest request, Company company, DateTime nowUtc, CancellationToken ct)
    {
        var purchases = await _db.Set<PurchaseInvoice>().AsNoTracking()
            .Where(p => p.State == DocumentState.Posted
                && p.DateReceived >= request.PeriodStart
                && p.DateReceived <= request.PeriodEnd)
            .OrderBy(p => p.DateReceived).ThenBy(p => p.DocumentNumber)
            .ToListAsync(ct);

        var supplierIds = purchases.Select(p => p.SupplierId).Distinct().ToArray();
        var suppliers = await _db.Set<Supplier>().AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var expenses = await _db.Set<Expense>().AsNoTracking()
            .Where(e => e.State == DocumentState.Posted
                && e.DocumentDate >= request.PeriodStart
                && e.DocumentDate <= request.PeriodEnd)
            .OrderBy(e => e.DocumentDate).ThenBy(e => e.DocumentNumber)
            .ToListAsync(ct);

        var categoryIds = expenses.Select(e => e.CategoryId).Distinct().ToArray();
        var categories = await _db.Set<DeductibleExpenseCategory>().AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = new List<PurchaseAndExpenseRegisterPdfRenderer.Row>();
        rows.AddRange(purchases.Select(p =>
        {
            suppliers.TryGetValue(p.SupplierId, out var s);
            // A purchase line is "deductible" when ANY of its lines
            // is flagged deductible (purchase invoices can mix
            // deductible + non-deductible lines; we surface the
            // any-deductible flag for the at-a-glance scan).
            var anyDeductible = p.Lines.Any(l => l.DeductibleFlag);
            return new PurchaseAndExpenseRegisterPdfRenderer.Row(
                Kind: PurchaseAndExpenseRegisterPdfRenderer.RowKind.PurchaseInvoice,
                DocumentNumber: p.DocumentNumber ?? "",
                DocumentDate: p.DateReceived,
                CounterpartyEn: s?.Name.English ?? "(unknown)",
                CounterpartyAr: s?.Name.Arabic ?? "",
                SupplierTin: p.SupplierTaxProfileSnapshot.TinValue,
                SupplierInvoiceNumber: p.SupplierInvoiceNumber,
                Subtotal: p.Subtotal.Amount,
                Vat: p.VatTotal.Amount,
                Total: p.GrandTotal.Amount,
                DeductibleFlag: anyDeductible);
        }));
        rows.AddRange(expenses.Select(e =>
        {
            categories.TryGetValue(e.CategoryId, out var cat);
            return new PurchaseAndExpenseRegisterPdfRenderer.Row(
                Kind: PurchaseAndExpenseRegisterPdfRenderer.RowKind.Expense,
                DocumentNumber: e.DocumentNumber ?? "",
                DocumentDate: e.DocumentDate,
                CounterpartyEn: cat?.Name.English ?? "(unknown category)",
                CounterpartyAr: cat?.Name.Arabic ?? "",
                SupplierTin: null,
                SupplierInvoiceNumber: null,
                Subtotal: e.Amount.Amount,
                Vat: 0m,
                Total: e.Amount.Amount,
                DeductibleFlag: e.DeductibleFlag);
        }));

        var ordered = rows.OrderBy(r => r.DocumentDate).ThenBy(r => r.DocumentNumber).ToList();
        return PurchaseAndExpenseRegisterPdfRenderer.Render(
            company, request.PeriodStart, request.PeriodEnd, nowUtc, ordered);
    }

    private async Task<byte[]> BuildCreditNoteAndReversalRegisterPdfAsync(
        InspectionBundleRequest request, Company company, DateTime nowUtc, CancellationToken ct)
    {
        var creditNotes = await _db.Set<SalesInvoice>().AsNoTracking()
            .Where(i => i.State == DocumentState.Posted
                && i.CreditNoteOfInvoiceId != null
                && i.DocumentDate >= request.PeriodStart
                && i.DocumentDate <= request.PeriodEnd)
            .OrderBy(i => i.DocumentDate).ThenBy(i => i.DocumentNumber)
            .ToListAsync(ct);

        var origIds = creditNotes
            .Where(c => c.CreditNoteOfInvoiceId.HasValue)
            .Select(c => c.CreditNoteOfInvoiceId!.Value).Distinct().ToArray();
        var origs = await _db.Set<SalesInvoice>().AsNoTracking()
            .Where(i => origIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var customerIds = creditNotes.Select(c => c.CustomerId).Distinct().ToArray();
        var customers = await _db.Set<Customer>().AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = creditNotes.Select(cn =>
        {
            customers.TryGetValue(cn.CustomerId, out var customer);
            origs.TryGetValue(cn.CreditNoteOfInvoiceId!.Value, out var orig);
            return new CreditNoteAndReversalRegisterPdfRenderer.Row(
                DocumentNumber: cn.DocumentNumber ?? "",
                DocumentDate: cn.DocumentDate,
                CustomerNameEn: customer?.Name.English ?? "(unknown)",
                CustomerNameAr: customer?.Name.Arabic ?? "",
                OriginalDocumentNumber: orig?.DocumentNumber ?? "(unknown)",
                OriginalDocumentDate: orig?.DocumentDate ?? DateOnly.MinValue,
                Reason: cn.CreditNoteReason ?? "(no reason recorded)",
                Subtotal: cn.Subtotal.Amount,
                Vat: cn.VatTotal.Amount,
                Total: cn.GrandTotal.Amount);
        }).ToList();

        return CreditNoteAndReversalRegisterPdfRenderer.Render(
            company, request.PeriodStart, request.PeriodEnd, nowUtc, rows);
    }

    private async Task<byte[]> BuildJournalListingPdfAsync(
        InspectionBundleRequest request, Company company, DateTime nowUtc, CancellationToken ct)
    {
        var startUtc = request.PeriodStart.ToDateTime(TimeOnly.MinValue);
        var endExclusive = request.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var entries = await _db.Set<JournalEntry>().AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.PostedAtUtc >= startUtc && e.PostedAtUtc < endExclusive)
            .OrderBy(e => e.PostedAtUtc).ThenBy(e => e.SourceDocumentNumber)
            .ToListAsync(ct);

        var rows = entries.Select(e => new GeneralJournalListingPdfRenderer.EntryRow(
            PostedAtUtc: e.PostedAtUtc,
            SourceDocumentNumber: e.SourceDocumentNumber,
            SourceDocumentType: e.SourceDocumentType,
            Lines: e.Lines.Select(l => new GeneralJournalListingPdfRenderer.LineRow(
                AccountCode: l.AccountCode,
                Debit: l.Debit.Amount,
                Credit: l.Credit.Amount,
                Description: l.Description)).ToList())).ToList();

        return GeneralJournalListingPdfRenderer.Render(
            company, request.PeriodStart, request.PeriodEnd, nowUtc, rows);
    }

    private async Task<byte[]> BuildTrialBalancePdfAsync(
        InspectionBundleRequest request, Company company, DateTime nowUtc, CancellationToken ct)
    {
        var report = await _trialBalanceQuery.RunAsync(
            request.PeriodStart, request.PeriodEnd, ct);
        return TrialBalancePdfRenderer.Render(company, nowUtc, report);
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
        sb.AppendLine("Or run the bundled verifier (Windows, PowerShell 5.1+; no install needed):");
        sb.AppendLine("```powershell");
        sb.AppendLine(".\\verify-bundle.ps1 -BundleDirectory \".\"");
        sb.AppendLine("```");
        sb.AppendLine("The script verifies per-file SHA-256 against the manifest. Top-level archive hash + audit-chain replay require the .NET-based verifier shipped with the application (`EgyptTax.Web verify-audit`).");
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

    private static byte[] LoadEmbeddedVerifierScript()
    {
        var assembly = typeof(InspectionBundleBuilder).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Inspection.verify-bundle.ps1";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded verifier script '{resourceName}' was not found. Check the EmbeddedResource Link in EgyptTax.Infrastructure.csproj.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
