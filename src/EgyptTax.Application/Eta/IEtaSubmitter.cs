using EgyptTax.Domain.Eta;

namespace EgyptTax.Application.Eta;

/// <summary>
/// FR-035 — port over the ETA submission transport. Production wires
/// a real-ETA HTTP client; the MVP wires <c>MockEtaSubmitter</c> which
/// simulates the regulator's response with a configurable failure
/// rate so the dashboard exercises both the Submitted and Failed
/// branches in dev / test. The submitter is responsible for the
/// transport call only — applying the outcome to the
/// <see cref="EtaSubmission"/> row + emitting the FR-028 audit event
/// is the orchestrator's job.
/// </summary>
public interface IEtaSubmitter
{
    /// <summary>
    /// Submit the canonical eInvoice JSON for the supplied invoice
    /// against the (mock or real) ETA endpoint. Returns the outcome
    /// the orchestrator records via <see cref="EtaSubmission.RecordAttempt"/>.
    /// </summary>
    Task<EtaSubmissionAttemptResult> SubmitAsync(
        Guid salesInvoiceId,
        string eInvoiceJson,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Outcome of one submit attempt. <see cref="OutcomeStatus"/> is one
/// of <see cref="EtaSubmissionStatus.Submitted"/> or
/// <see cref="EtaSubmissionStatus.Failed"/> — the
/// <see cref="EtaSubmissionStatus.Pending"/> initial state is owned by
/// <c>PostSalesInvoiceHandler</c> and is never the result of an
/// attempt.
/// </summary>
public sealed record EtaSubmissionAttemptResult(
    EtaSubmissionStatus OutcomeStatus,
    string? SubmissionUuid,
    string? ErrorCode,
    string? ErrorMessage
);
