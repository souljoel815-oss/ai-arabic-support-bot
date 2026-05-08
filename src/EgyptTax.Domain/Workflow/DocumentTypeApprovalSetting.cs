namespace EgyptTax.Domain.Workflow;

/// <summary>
/// FR-026 — per-document-type approval toggle. When
/// <see cref="ApprovalRequired"/> is false, the document type permits
/// the Draft → Posted direct-post path (recorded in the audit log as
/// <c>unapproved-direct</c>); when true, the document follows the full
/// Draft → Submitted → Approved → Posted lifecycle with FR-004 (no
/// self-approval) applied. The Phase-1 default seed is
/// <c>SalesInvoice → false</c> so US1 ships demoable; all other types
/// default to <c>true</c>.
/// </summary>
public sealed class DocumentTypeApprovalSetting
{
    public DocumentType DocumentType { get; init; }
    public bool ApprovalRequired { get; private set; }

    private DocumentTypeApprovalSetting() { }

    public DocumentTypeApprovalSetting(DocumentType documentType, bool approvalRequired)
    {
        DocumentType = documentType;
        ApprovalRequired = approvalRequired;
    }

    public void SetApprovalRequired(bool approvalRequired) => ApprovalRequired = approvalRequired;
}
