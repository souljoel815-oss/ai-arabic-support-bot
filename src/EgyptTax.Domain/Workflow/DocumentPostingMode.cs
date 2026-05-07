namespace EgyptTax.Domain.Workflow;

/// <summary>
/// FR-026 — captured at post-time on every aggregate. Distinguishes
/// the two FR-026 transition paths so an auditor can tell whether
/// the post followed the full Draft → Submitted → Approved → Posted
/// lifecycle or skipped approval because the document type permits
/// direct posting (per <c>DocumentTypeApprovalSetting.ApprovalRequired
/// = false</c>; the Phase-1 default has SalesInvoice direct-post).
/// </summary>
public enum DocumentPostingMode
{
    ApprovedThenPosted,
    UnapprovedDirect,
}
