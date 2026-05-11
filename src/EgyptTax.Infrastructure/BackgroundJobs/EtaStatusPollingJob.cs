using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// P1.3 — Hangfire-driven poller that asks the regulator's "Get
/// Document" endpoint about every Submitted-but-not-yet-acknowledged
/// row, and advances the row's lifecycle:
///
///  * <c>PendingAcknowledgement</c> → no change, will be retried on
///    the next tick.
///  * <c>Acknowledged</c> with a long UUID → calls
///    <see cref="EtaSubmission.RecordRegulatorAcknowledgement"/> and
///    emits an FR-028 audit event so downstream firms (and the
///    operator's mailbox) know the document is now citable.
///  * <c>Rejected</c> → calls
///    <see cref="EtaSubmission.RecordRegulatorRejection"/> which
///    transitions the row back to <c>Failed</c> with the
///    regulator's verbatim reason; the next tick of the existing
///    <see cref="EtaSubmissionRetryJob"/> will pick it up only if
///    the failure is transport-level — a regulator rejection is
///    NOT auto-retried (the catalog of rejections that DO warrant
///    a retry is the operator's call).
///
/// Cap on polling: rows with non-null
/// <see cref="EtaSubmission.RegulatorAcknowledgedAtUtc"/>,
/// non-null <see cref="EtaSubmission.RegulatorRejectedAtUtc"/>, or
/// past their submission window are skipped — the poller only does
/// work that can change the row's state.
/// </summary>
public sealed class EtaStatusPollingJob
{
    private readonly AppDbContext _db;
    private readonly IEtaStatusQuery _statusQuery;
    private readonly IEtaStatusNotifier _notifier;
    private readonly IAuditLogStore _auditLog;
    private readonly IClock _clock;

    public EtaStatusPollingJob(
        AppDbContext db,
        IEtaStatusQuery statusQuery,
        IEtaStatusNotifier notifier,
        IAuditLogStore auditLog,
        IClock clock)
    {
        _db = db;
        _statusQuery = statusQuery;
        _notifier = notifier;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<EtaPollingJobResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;

        var candidates = await _db.Set<EtaSubmission>()
            .Where(s =>
                s.Status == EtaSubmissionStatus.Submitted
                && s.SubmissionUuid != null
                && s.RegulatorAcknowledgedAtUtc == null
                && s.RegulatorRejectedAtUtc == null
                && s.SubmissionWindowExpiresAtUtc > nowUtc)
            .OrderBy(s => s.LastAttemptAtUtc ?? s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var acknowledged = 0;
        var rejected = 0;
        var stillPending = 0;

        // Project document numbers up front so the notifier can carry
        // them without re-querying once per row.
        var invoiceIds = candidates.Select(c => c.SalesInvoiceId).ToArray();
        var docNumbers = await _db.Set<SalesInvoice>()
            .AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .Select(i => new { i.Id, i.DocumentNumber })
            .ToDictionaryAsync(i => i.Id, i => i.DocumentNumber ?? "", cancellationToken);

        foreach (var row in candidates)
        {
            EtaDocumentStatusResult result;
            try
            {
                result = await _statusQuery.GetStatusAsync(row.SubmissionUuid!, cancellationToken);
            }
            catch
            {
                // One bad poll shouldn't poison the batch — the next
                // tick will try again.
                continue;
            }

            var docNumber = docNumbers.TryGetValue(row.SalesInvoiceId, out var n) ? n : "";

            switch (result.Status)
            {
                case EtaDocumentStatus.Acknowledged:
                    if (string.IsNullOrWhiteSpace(result.RegulatorLongUuid)) continue;
                    row.RecordRegulatorAcknowledgement(result.RegulatorLongUuid, _clock.UtcNow);
                    await _db.SaveChangesAsync(cancellationToken);
                    await EmitAcknowledgedAsync(row, result.RegulatorLongUuid, cancellationToken);
                    await NotifyAsync(row, docNumber, EtaSubmissionStatus.Submitted, cancellationToken);
                    acknowledged++;
                    break;

                case EtaDocumentStatus.Rejected:
                    row.RecordRegulatorRejection(
                        result.ErrorCode ?? "ETA_REGULATOR_REJECTED",
                        result.ErrorMessage ?? "Regulator rejected the document with no reason text.",
                        _clock.UtcNow);
                    await _db.SaveChangesAsync(cancellationToken);
                    await EmitRejectedAsync(row, cancellationToken);
                    await NotifyAsync(row, docNumber, EtaSubmissionStatus.Submitted, cancellationToken);
                    rejected++;
                    break;

                case EtaDocumentStatus.PendingAcknowledgement:
                default:
                    stillPending++;
                    break;
            }
        }

        return new EtaPollingJobResult(
            TotalCandidates: candidates.Count,
            AcknowledgedCount: acknowledged,
            RejectedCount: rejected,
            StillPendingCount: stillPending);
    }

    private async Task NotifyAsync(
        EtaSubmission row,
        string docNumber,
        EtaSubmissionStatus previousStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.NotifyAsync(
                new EtaStatusChangedEvent(
                    SalesInvoiceId: row.SalesInvoiceId,
                    EtaSubmissionId: row.Id,
                    DocumentNumber: docNumber,
                    PreviousStatus: previousStatus,
                    NewStatus: row.Status,
                    AttemptCount: row.AttemptCount,
                    AtUtc: _clock.UtcNow),
                cancellationToken);
        }
        catch
        {
            // Notifier failures must never abort the batch — the
            // dashboard will catch the change on its next refresh.
        }
    }

    private async Task EmitAcknowledgedAsync(
        EtaSubmission row,
        string longUuid,
        CancellationToken cancellationToken)
    {
        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "eta_submission.regulator_acknowledged",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"eta_submission_id":"{{row.Id:D}}","sales_invoice_id":"{{row.SalesInvoiceId:D}}","short_uuid":"{{row.SubmissionUuid}}","regulator_long_uuid":"{{longUuid}}"}"""
            ),
            cancellationToken);
    }

    private async Task EmitRejectedAsync(EtaSubmission row, CancellationToken cancellationToken)
    {
        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "eta_submission.regulator_rejected",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"eta_submission_id":"{{row.Id:D}}","sales_invoice_id":"{{row.SalesInvoiceId:D}}","short_uuid":"{{row.SubmissionUuid}}","error_code":{{(row.ErrorCode is null ? "null" : "\"" + row.ErrorCode + "\"")}},"attempt_count":{{row.AttemptCount.ToString(CultureInfo.InvariantCulture)}}}"""
            ),
            cancellationToken);
    }
}

/// <summary>
/// Outcome of one <see cref="EtaStatusPollingJob.RunOnceAsync"/>
/// tick. Surfaced for observability + tests.
/// </summary>
public sealed record EtaPollingJobResult(
    int TotalCandidates,
    int AcknowledgedCount,
    int RejectedCount,
    int StillPendingCount);
