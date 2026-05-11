namespace EgyptTax.Application.Eta;

/// <summary>
/// P1.3 — port over the ETA "Get Document" lookup. The submission
/// flow on the regulator side is asynchronous: <c>POST /documents/submit</c>
/// returns a short submission UUID acknowledging receipt, then the
/// regulator validates the document over the next minutes-to-hours
/// and (when valid) issues a long, citable document UUID. This port
/// is consumed by <c>EtaStatusPollingJob</c> which advances
/// <see cref="EgyptTax.Domain.Eta.EtaSubmission"/> rows from
/// "Submitted (waiting for regulator)" to either "Acknowledged
/// (long UUID issued)" or "Failed (regulator rejected)".
///
/// Production wires a real-ETA HTTP client; the MVP wires
/// <c>MockEtaStatusQuery</c> which simulates the regulator's
/// validation latency + outcome distribution.
/// </summary>
public interface IEtaStatusQuery
{
    /// <summary>
    /// Look up the regulator-side status of a previously-submitted
    /// document by its short submission UUID. Returns
    /// <c>PendingAcknowledgement</c> while the regulator is still
    /// validating; <c>Acknowledged</c> with a long UUID once the
    /// document is accepted; <c>Rejected</c> with an error reason
    /// when the regulator decides the document is invalid.
    /// </summary>
    Task<EtaDocumentStatusResult> GetStatusAsync(
        string submissionUuid,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Result of one <see cref="IEtaStatusQuery.GetStatusAsync"/> call.
/// Distinct from <c>EtaSubmissionAttemptResult</c> because the
/// outcome space is different — there's no transient-failure case
/// (the GET is idempotent).
/// </summary>
public sealed record EtaDocumentStatusResult(
    EtaDocumentStatus Status,
    string? RegulatorLongUuid,
    string? ErrorCode,
    string? ErrorMessage
);

public enum EtaDocumentStatus
{
    /// <summary>Regulator hasn't finished validating yet — poll again later.</summary>
    PendingAcknowledgement,

    /// <summary>Regulator accepted the document — long UUID issued.</summary>
    Acknowledged,

    /// <summary>Regulator rejected the document — operator must correct + resubmit.</summary>
    Rejected,
}
