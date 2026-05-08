using EgyptTax.Domain.Workflow;

namespace EgyptTax.Application.Workflow;

/// <summary>
/// FR-026 — Bookkeeper transitions a Draft document into the
/// approval queue. Caller resolves the document type via
/// <see cref="DocumentType"/> so a single handler covers all
/// approval-eligible aggregates (SalesInvoice / PurchaseInvoice /
/// Expense for the MVP).
/// </summary>
public sealed record SubmitDocumentCommand(
    Guid DocumentId,
    DocumentType DocumentType,
    Guid SubmittedByUserId
);

/// <summary>FR-026 — Approver moves Submitted → Approved.</summary>
public sealed record ApproveDocumentCommand(
    Guid DocumentId,
    DocumentType DocumentType,
    Guid ApprovedByUserId
);

/// <summary>
/// FR-026 — Approver rejects: state returns to Draft + the
/// ApprovalRequest row records the rejection reason. Reason is
/// required (operator needs to know why).
/// </summary>
public sealed record RejectDocumentCommand(
    Guid DocumentId,
    DocumentType DocumentType,
    Guid RejectedByUserId,
    string Reason
);

/// <summary>
/// FR-026 / FR-027 — void a non-Posted document. Posted documents
/// cannot be voided; corrections route through credit notes
/// (FR-013) or reversal vouchers (US4).
/// </summary>
public sealed record VoidDocumentCommand(
    Guid DocumentId,
    DocumentType DocumentType,
    Guid VoidedByUserId,
    string? Reason
);
