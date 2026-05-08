namespace EgyptTax.Domain.Workflow;

/// <summary>
/// E2 / FR-026 / FR-004 — one row per Submit call. Tracks who
/// submitted the document, who approved or rejected it, and the
/// rejection reason. Status is implicit in the populated nullable
/// fields:
///   * Pending  — both Approved* and Rejected* fields null.
///   * Approved — Approved* populated, Rejected* null.
///   * Rejected — Rejected* populated, Approved* null.
///
/// Invariant FR-004: <see cref="ApprovedByUserId"/> and
/// <see cref="RejectedByUserId"/> MUST differ from
/// <see cref="SubmittedByUserId"/>. Enforced by the application
/// handler (the guard sees the actor before mutating the row).
/// </summary>
public sealed class ApprovalRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DocumentId { get; init; }
    public DocumentType DocumentType { get; init; }
    public Guid SubmittedByUserId { get; init; }
    public DateTime SubmittedAtUtc { get; init; }

    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    public ApprovalRequestStatus Status =>
        ApprovedByUserId is not null ? ApprovalRequestStatus.Approved
        : RejectedByUserId is not null ? ApprovalRequestStatus.Rejected
        : ApprovalRequestStatus.Pending;

    private ApprovalRequest() { }

    public ApprovalRequest(
        Guid documentId,
        DocumentType documentType,
        Guid submittedByUserId,
        DateTime submittedAtUtc)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("DocumentId is required.", nameof(documentId));
        }
        if (submittedByUserId == Guid.Empty)
        {
            throw new ArgumentException("SubmittedByUserId is required.", nameof(submittedByUserId));
        }
        DocumentId = documentId;
        DocumentType = documentType;
        SubmittedByUserId = submittedByUserId;
        SubmittedAtUtc = submittedAtUtc;
    }

    public void RecordApproval(Guid approverUserId, DateTime utcNow)
    {
        if (Status != ApprovalRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Approval request {Id} is already {Status}; cannot approve.");
        }
        if (approverUserId == SubmittedByUserId)
        {
            throw new InvalidOperationException(
                "FR-004: a user cannot approve their own submission.");
        }
        ApprovedByUserId = approverUserId;
        ApprovedAtUtc = utcNow;
    }

    public void RecordRejection(Guid rejecterUserId, DateTime utcNow, string reason)
    {
        if (Status != ApprovalRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Approval request {Id} is already {Status}; cannot reject.");
        }
        if (rejecterUserId == SubmittedByUserId)
        {
            throw new InvalidOperationException(
                "FR-004: a user cannot reject their own submission.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RejectedByUserId = rejecterUserId;
        RejectedAtUtc = utcNow;
        RejectionReason = reason;
    }
}

public enum ApprovalRequestStatus
{
    Pending,
    Approved,
    Rejected,
}
