using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Eta;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Invoices;

/// <summary>
/// FR-035 — orchestrator that wraps <see cref="PostSalesInvoiceHandler"/>
/// with an inline ETA submission attempt. The bare PostSalesInvoiceHandler
/// remains the canonical post path (creates the invoice in Posted state
/// + the EtaSubmission row in Pending state); this wrapper then renders
/// the eInvoice JSON, calls <see cref="IEtaSubmitter"/>, applies the
/// outcome to the EtaSubmission row via <see cref="EtaSubmission.RecordAttempt"/>,
/// and emits an FR-028 audit event. The MVP runs synchronously (no
/// Hangfire fan-out) so the post-flow returns deterministically; a
/// future T094-follow-on can lift this to a fire-and-forget Hangfire
/// job once retries-on-Failed land. The wrapper's caller (the Web
/// editor's Post button) gets back the same SalesInvoice the bare
/// handler returns + the ETA row already shows the attempt outcome
/// when the user lands on the detail page.
/// </summary>
public sealed class PostSalesInvoiceWithEtaSubmissionHandler
{
    private readonly PostSalesInvoiceHandler _innerHandler;
    private readonly AppDbContext _db;
    private readonly IEtaSubmitter _submitter;
    private readonly IEInvoiceJsonGenerator _jsonGenerator;
    private readonly IAuditLogStore _auditLog;
    private readonly IClock _clock;
    private readonly IEtaStatusNotifier? _notifier;

    public PostSalesInvoiceWithEtaSubmissionHandler(
        PostSalesInvoiceHandler innerHandler,
        AppDbContext db,
        IEtaSubmitter submitter,
        IEInvoiceJsonGenerator jsonGenerator,
        IAuditLogStore auditLog,
        IClock clock,
        IEtaStatusNotifier? notifier = null
    )
    {
        _innerHandler = innerHandler;
        _db = db;
        _submitter = submitter;
        _jsonGenerator = jsonGenerator;
        _auditLog = auditLog;
        _clock = clock;
        _notifier = notifier;
    }

    public async Task<SalesInvoice> HandleAsync(
        PostSalesInvoiceCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var invoice = await _innerHandler.HandleAsync(command, cancellationToken);

        var bundle = await InvoiceRenderingPipeline.LoadAsync(_db, invoice.Id, cancellationToken);
        var etaRow = await _db.Set<EtaSubmission>()
            .FirstOrDefaultAsync(s => s.SalesInvoiceId == invoice.Id, cancellationToken);

        if (bundle is null || etaRow is null)
        {
            // Either the issuer Company isn't seeded yet (bundle null
            // because LoadAsync requires it) or PostSalesInvoiceHandler
            // didn't create the EtaSubmission row. Either way, we cannot
            // submit; surface the post itself as Posted-but-unsubmitted
            // and leave the dashboard's Pending tile to show it.
            return invoice;
        }

        var json = _jsonGenerator.GenerateAsJson(bundle.EInvoiceRequest);
        var attempt = await _submitter.SubmitAsync(invoice.Id, json, cancellationToken);

        etaRow.RecordAttempt(
            status: attempt.OutcomeStatus,
            submissionUuid: attempt.SubmissionUuid,
            errorCode: attempt.ErrorCode,
            errorMessage: attempt.ErrorMessage,
            nowUtc: _clock.UtcNow
        );
        await _db.SaveChangesAsync(cancellationToken);

#pragma warning disable CA1308 // Audit-event kind names use lowercase by convention; CA1308's uppercase guidance does not apply to opaque event identifiers.
        var kindSuffix = attempt.OutcomeStatus.ToString().ToLowerInvariant();
#pragma warning restore CA1308

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: $"eta_submission.{kindSuffix}",
                ActorUserId: command.PostedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildAuditPayload(invoice, etaRow, attempt)
            ),
            cancellationToken
        );

        // T125 — broadcast the status change so the dashboard can
        // refresh live without polling. Notifier is optional (the
        // existing T080-T083 tests construct the handler without it
        // and the field is nullable); when wired, fan-out goes to
        // both SignalR clients + in-process Blazor subscribers.
        if (_notifier is not null)
        {
            await _notifier.NotifyAsync(
                new EtaStatusChangedEvent(
                    SalesInvoiceId: invoice.Id,
                    EtaSubmissionId: etaRow.Id,
                    DocumentNumber: invoice.DocumentNumber!,
                    PreviousStatus: EtaSubmissionStatus.Pending,
                    NewStatus: attempt.OutcomeStatus,
                    AttemptCount: etaRow.AttemptCount,
                    AtUtc: _clock.UtcNow
                ),
                cancellationToken
            );
        }

        return invoice;
    }

    private static string BuildAuditPayload(
        SalesInvoice invoice,
        EtaSubmission row,
        EtaSubmissionAttemptResult attempt
    ) =>
        $$"""{"invoice_id":"{{invoice.Id:D}}","eta_submission_id":"{{row.Id:D}}","document_number":"{{invoice.DocumentNumber}}","outcome":"{{attempt.OutcomeStatus}}","submission_uuid":{{(attempt.SubmissionUuid is null ? "null" : "\"" + attempt.SubmissionUuid + "\"")}},"error_code":{{(attempt.ErrorCode is null ? "null" : "\"" + attempt.ErrorCode + "\"")}},"attempt_count":{{row.AttemptCount.ToString(CultureInfo.InvariantCulture)}}}""";
}
