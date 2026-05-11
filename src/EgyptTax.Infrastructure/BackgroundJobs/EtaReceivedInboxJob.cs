using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// P1.5 — Hangfire job that polls the regulator's "Get Received
/// Documents" feed and inserts new envelopes into
/// <c>eta.eta_received_documents</c> for the operator's inbox
/// queue. Idempotent: a document already in the table (matched by
/// regulator long UUID) is skipped, so the job can run as
/// frequently as we like without producing dupes.
///
/// Cron default is daily at 06:00 (operator opens the laptop and
/// the inbox is already populated from overnight); production
/// installs that need fresher data drop to hourly. The job emits
/// one FR-028 audit event per inserted document so the chain
/// records exactly when each supplier's invoice first appeared in
/// our books.
/// </summary>
public sealed class EtaReceivedInboxJob
{
    private readonly AppDbContext _db;
    private readonly IEtaReceivedDocumentSource _source;
    private readonly IAuditLogStore _auditLog;
    private readonly IClock _clock;

    public EtaReceivedInboxJob(
        AppDbContext db,
        IEtaReceivedDocumentSource source,
        IAuditLogStore auditLog,
        IClock clock)
    {
        _db = db;
        _source = source;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<EtaInboxJobResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        // First run: pull the last 30 days. Subsequent runs: from the
        // newest document we already have. The dedupe by long UUID
        // makes overlap safe but trimming the window keeps payload
        // size bounded.
        var lastSeen = await _db.Set<EtaReceivedDocument>()
            .OrderByDescending(d => d.FirstSeenAtUtc)
            .Select(d => (DateTime?)d.FirstSeenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var sinceUtc = lastSeen ?? nowUtc.AddDays(-30);

        var envelopes = await _source.FetchSinceAsync(sinceUtc, cancellationToken);

        // Bulk dedupe: load existing UUIDs for the candidate set in
        // one round-trip rather than N selects.
        var candidateUuids = envelopes.Select(e => e.RegulatorLongUuid).ToArray();
        var existing = await _db.Set<EtaReceivedDocument>()
            .Where(d => candidateUuids.Contains(d.RegulatorLongUuid))
            .Select(d => d.RegulatorLongUuid)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        var inserted = 0;
        var skipped = 0;
        foreach (var env in envelopes)
        {
            if (existingSet.Contains(env.RegulatorLongUuid))
            {
                skipped++;
                continue;
            }
            var row = new EtaReceivedDocument(
                regulatorLongUuid: env.RegulatorLongUuid,
                supplierTin: env.SupplierTin,
                supplierLegalName: env.SupplierLegalName,
                documentNumber: env.DocumentNumber,
                documentDate: env.DocumentDate,
                netBeforeVatEgp: env.NetBeforeVatEgp,
                vatTotalEgp: env.VatTotalEgp,
                grandTotalEgp: env.GrandTotalEgp,
                firstSeenAtUtc: nowUtc);
            _db.Add(row);
            inserted++;

            await _auditLog.AppendAsync(
                new AuditLogPayload(
                    Kind: "eta_received.inbox_pulled",
                    ActorUserId: null,
                    ActorFirmName: null,
                    CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"regulator_long_uuid":"{{env.RegulatorLongUuid}}","supplier_tin":"{{env.SupplierTin}}","document_number":"{{env.DocumentNumber}}","grand_total_egp":{{env.GrandTotalEgp.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""
                ),
                cancellationToken);
        }

        if (inserted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new EtaInboxJobResult(
            FetchedCount: envelopes.Count,
            InsertedCount: inserted,
            SkippedExistingCount: skipped);
    }
}

public sealed record EtaInboxJobResult(int FetchedCount, int InsertedCount, int SkippedExistingCount);
