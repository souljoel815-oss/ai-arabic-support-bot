using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// FR-036 — Hangfire-driven retry of failed ETA submissions whose
/// regulator-imposed window is still open. Fired on a recurring cron
/// schedule (default every 15 min); each tick:
///
///  * Selects every <see cref="EtaSubmission"/> with
///    <c>Status == Failed</c> AND <c>SubmissionWindowExpiresAtUtc &gt;
///    nowUtc</c>, ordered by deadline ascending so the most-urgent
///    rows are retried first.
///  * <c>Submitted</c> rows are filtered out by the status equality
///    so the never-re-submit invariant holds without relying on the
///    <see cref="EtaSubmission.RecordAttempt"/> guard.
///  * <c>Pending</c> rows are NOT picked up — a never-attempted row
///    is owned by the post-time wrapper handler; it isn't a "retry"
///    candidate.
///  * Expired-window <c>Failed</c> rows are NOT picked up — the
///    regulator's deadline has passed; the operator must intervene
///    manually (the dashboard surfaces them in a dedicated tile).
///
/// For each retried row the job loads the invoice + dependencies
/// via <see cref="InvoiceRenderingPipeline"/>, generates the
/// canonical eInvoice JSON, calls <see cref="IEtaSubmitter"/>, and
/// records the outcome via
/// <see cref="EtaSubmission.RecordAttempt"/> — increments the
/// attempt count, persists submission UUID / error code / last
/// attempt timestamp, and transitions the status. Three FR-028
/// audit events fire per row: <c>retry_attempted</c> before the
/// submit, then either <c>retry_submitted</c> (success) or
/// <c>retry_failed</c> (failure). The job keeps the per-row work
/// independent — one submitter throw doesn't abort the rest of the
/// batch.
/// </summary>
public sealed class EtaSubmissionRetryJob
{
    private readonly AppDbContext _db;
    private readonly IEtaSubmitter _submitter;
    private readonly IEInvoiceJsonGenerator _jsonGenerator;
    private readonly IAuditLogStore _auditLog;
    private readonly IClock _clock;

    public EtaSubmissionRetryJob(
        AppDbContext db,
        IEtaSubmitter submitter,
        IEInvoiceJsonGenerator jsonGenerator,
        IAuditLogStore auditLog,
        IClock clock
    )
    {
        _db = db;
        _submitter = submitter;
        _jsonGenerator = jsonGenerator;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<EtaRetryJobResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;

        var candidates = await _db.Set<EtaSubmission>()
            .Where(s =>
                s.Status == EtaSubmissionStatus.Failed && s.SubmissionWindowExpiresAtUtc > nowUtc
            )
            .OrderBy(s => s.SubmissionWindowExpiresAtUtc)
            .ToListAsync(cancellationToken);

        var succeeded = 0;
        var failed = 0;

        foreach (var row in candidates)
        {
            await EmitRetryAttemptedAsync(row, cancellationToken);

            var bundle = await InvoiceRenderingPipeline.LoadAsync(
                _db,
                row.SalesInvoiceId,
                cancellationToken
            );
            if (bundle is null)
            {
                // The invoice or its issuer / receiver is missing — we
                // can't generate the JSON. Log a synthetic failure
                // attempt so the row's attempt counter advances and an
                // operator sees the issue on the dashboard.
                row.RecordAttempt(
                    EtaSubmissionStatus.Failed,
                    submissionUuid: null,
                    errorCode: "ETA_RETRY_HYDRATE_MISSING",
                    errorMessage: "Could not load invoice + dependencies for retry. Check Company / Customer rows.",
                    nowUtc: _clock.UtcNow
                );
                await _db.SaveChangesAsync(cancellationToken);
                await EmitRetryFailedAsync(row, cancellationToken);
                failed++;
                continue;
            }

            var json = _jsonGenerator.GenerateAsJson(bundle.EInvoiceRequest);
            var attempt = await _submitter.SubmitAsync(row.SalesInvoiceId, json, cancellationToken);

            row.RecordAttempt(
                status: attempt.OutcomeStatus,
                submissionUuid: attempt.SubmissionUuid,
                errorCode: attempt.ErrorCode,
                errorMessage: attempt.ErrorMessage,
                nowUtc: _clock.UtcNow
            );
            await _db.SaveChangesAsync(cancellationToken);

            if (attempt.OutcomeStatus == EtaSubmissionStatus.Submitted)
            {
                await EmitRetrySubmittedAsync(row, attempt, cancellationToken);
                succeeded++;
            }
            else
            {
                await EmitRetryFailedAsync(row, cancellationToken);
                failed++;
            }
        }

        return new EtaRetryJobResult(
            TotalCandidates: candidates.Count,
            SucceededCount: succeeded,
            FailedCount: failed
        );
    }

    private async Task EmitRetryAttemptedAsync(
        EtaSubmission row,
        CancellationToken cancellationToken
    )
    {
        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "eta_submission.retry_attempted",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"eta_submission_id":"{{row.Id:D}}","sales_invoice_id":"{{row.SalesInvoiceId:D}}","attempt_count_before":{{row.AttemptCount.ToString(CultureInfo.InvariantCulture)}},"deadline_utc":"{{row.SubmissionWindowExpiresAtUtc:o}}"}"""
            ),
            cancellationToken
        );
    }

    private async Task EmitRetrySubmittedAsync(
        EtaSubmission row,
        EtaSubmissionAttemptResult attempt,
        CancellationToken cancellationToken
    )
    {
        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "eta_submission.retry_submitted",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"eta_submission_id":"{{row.Id:D}}","sales_invoice_id":"{{row.SalesInvoiceId:D}}","attempt_count":{{row.AttemptCount.ToString(CultureInfo.InvariantCulture)}},"submission_uuid":"{{attempt.SubmissionUuid}}"}"""
            ),
            cancellationToken
        );
    }

    private async Task EmitRetryFailedAsync(EtaSubmission row, CancellationToken cancellationToken)
    {
        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "eta_submission.retry_failed",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"eta_submission_id":"{{row.Id:D}}","sales_invoice_id":"{{row.SalesInvoiceId:D}}","attempt_count":{{row.AttemptCount.ToString(CultureInfo.InvariantCulture)}},"error_code":{{(row.ErrorCode is null ? "null" : "\"" + row.ErrorCode + "\"")}}}"""
            ),
            cancellationToken
        );
    }
}

/// <summary>
/// Outcome of one <see cref="EtaSubmissionRetryJob.RunOnceAsync"/>
/// tick. Surfaced from the job so observability + tests can assert
/// on which branches fired.
/// </summary>
public sealed record EtaRetryJobResult(int TotalCandidates, int SucceededCount, int FailedCount);
