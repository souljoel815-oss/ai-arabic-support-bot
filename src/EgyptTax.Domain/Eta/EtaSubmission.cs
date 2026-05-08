namespace EgyptTax.Domain.Eta;

/// <summary>
/// FR-035 — 1:1 partner of <c>SalesInvoice</c> when posted. Captures
/// the lifecycle of the document's submission to the (mock) Egyptian
/// Tax Authority eInvoice endpoint: deadline window, attempt history,
/// terminal status. The dashboard (FR-035 + SC-012) uses this row's
/// <see cref="SubmissionWindowExpiresAtUtc"/> + <see cref="Status"/>
/// to surface invoices nearing or past their deadline so the operator
/// can intervene before the regulator-imposed cutoff.
/// </summary>
public sealed class EtaSubmission
{
    /// <summary>FR-035 default — Egyptian regulator allows 7 days from issuance.</summary>
    public const int DefaultSubmissionWindowDays = 7;

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SalesInvoiceId { get; init; }

    public EtaSubmissionStatus Status { get; private set; } = EtaSubmissionStatus.Pending;
    public string? SubmissionUuid { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Wall-clock deadline by which the document MUST be submitted
    /// (default = posted_at + 7 days). The dashboard queries against
    /// this column with an index-friendly range filter.
    /// </summary>
    public DateTime SubmissionWindowExpiresAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    private EtaSubmission() { }

    public EtaSubmission(
        Guid salesInvoiceId,
        DateTime postedAtUtc,
        DateTime nowUtc,
        TimeSpan? submissionWindow = null
    )
    {
        if (salesInvoiceId == Guid.Empty)
        {
            throw new ArgumentException("SalesInvoiceId must be supplied.", nameof(salesInvoiceId));
        }

        SalesInvoiceId = salesInvoiceId;
        SubmissionWindowExpiresAtUtc = postedAtUtc.Add(
            submissionWindow ?? TimeSpan.FromDays(DefaultSubmissionWindowDays)
        );
        CreatedAtUtc = nowUtc;
    }

    public void RecordAttempt(
        EtaSubmissionStatus status,
        string? submissionUuid,
        string? errorCode,
        string? errorMessage,
        DateTime nowUtc
    )
    {
        if (Status == EtaSubmissionStatus.Submitted)
        {
            // Submitted is terminal — re-submitting an already-submitted
            // document is a programming error.
            throw new InvalidOperationException(
                $"EtaSubmission {Id} is already Submitted; cannot record another attempt."
            );
        }

        Status = status;
        SubmissionUuid = submissionUuid;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        LastAttemptAtUtc = nowUtc;
        AttemptCount++;
    }
}

/// <summary>
/// FR-035 — terminal status of an ETA submission. <c>Pending</c> is
/// the initial state when a document is posted; <c>Submitted</c> is
/// terminal (the mock returned a uuid); <c>Failed</c> is non-terminal
/// (retry allowed within the submission window).
/// </summary>
public enum EtaSubmissionStatus
{
    Pending,
    Submitted,
    Failed,
}
