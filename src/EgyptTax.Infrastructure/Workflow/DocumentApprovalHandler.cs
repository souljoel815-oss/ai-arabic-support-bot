using EgyptTax.Application.Audit;
using EgyptTax.Application.Workflow;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Workflow;

/// <summary>
/// FR-026 — single handler for the approval workflow's four
/// transitions (Submit / Approve / Reject / Void). Each method
/// dispatches on <see cref="DocumentType"/> to load the right
/// aggregate, calls the matching MarkXxx state-machine mutator,
/// updates the ApprovalRequest row (creating it on Submit), and
/// emits an FR-028 audit event.
///
/// FR-004 (no self-approval) is enforced inside
/// <see cref="ApprovalRequest.RecordApproval"/> +
/// <see cref="ApprovalRequest.RecordRejection"/>; throwing here is
/// the same exception the page catches and surfaces.
/// </summary>
public sealed class DocumentApprovalHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public DocumentApprovalHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<ApprovalRequest> SubmitAsync(
        SubmitDocumentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var nowUtc = _clock.UtcNow;

        await TransitionAsync(
            command.DocumentId,
            command.DocumentType,
            sales: s => s.MarkSubmitted(),
            purchase: p => p.MarkSubmitted(),
            expense: e => e.MarkSubmitted(),
            cancellationToken
        );

        var request = new ApprovalRequest(
            command.DocumentId,
            command.DocumentType,
            command.SubmittedByUserId,
            nowUtc
        );
        _db.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "document.submitted_for_approval",
                ActorUserId: command.SubmittedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"document_id":"{{command.DocumentId:D}}","document_type":"{{command.DocumentType}}","approval_request_id":"{{request.Id:D}}"}"""
            ),
            cancellationToken
        );

        return request;
    }

    public async Task<ApprovalRequest> ApproveAsync(
        ApproveDocumentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var nowUtc = _clock.UtcNow;

        var request = await LoadPendingApprovalAsync(command.DocumentId, cancellationToken);

        // FR-004 self-approval guard runs INSIDE RecordApproval — let
        // that throw before we touch the document state so the post
        // never partially completes.
        request.RecordApproval(command.ApprovedByUserId, nowUtc);

        await TransitionAsync(
            command.DocumentId,
            command.DocumentType,
            sales: s => s.MarkApproved(),
            purchase: p => p.MarkApproved(),
            expense: e => e.MarkApproved(),
            cancellationToken
        );

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "document.approved",
                ActorUserId: command.ApprovedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"document_id":"{{command.DocumentId:D}}","document_type":"{{command.DocumentType}}","approval_request_id":"{{request.Id:D}}","submitted_by":"{{request.SubmittedByUserId:D}}"}"""
            ),
            cancellationToken
        );

        return request;
    }

    public async Task<ApprovalRequest> RejectAsync(
        RejectDocumentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        var nowUtc = _clock.UtcNow;

        var request = await LoadPendingApprovalAsync(command.DocumentId, cancellationToken);
        request.RecordRejection(command.RejectedByUserId, nowUtc, command.Reason);

        await TransitionAsync(
            command.DocumentId,
            command.DocumentType,
            sales: s => s.MarkRejected(),
            purchase: p => p.MarkRejected(),
            expense: e => e.MarkRejected(),
            cancellationToken
        );

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "document.rejected",
                ActorUserId: command.RejectedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"document_id":"{{command.DocumentId:D}}","document_type":"{{command.DocumentType}}","approval_request_id":"{{request.Id:D}}","reason":"{{Escape(command.Reason)}}"}"""
            ),
            cancellationToken
        );

        return request;
    }

    public async Task VoidAsync(
        VoidDocumentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        await TransitionAsync(
            command.DocumentId,
            command.DocumentType,
            sales: s => s.MarkVoided(),
            purchase: p => p.MarkVoided(),
            expense: e => e.MarkVoided(),
            cancellationToken
        );

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "document.voided",
                ActorUserId: command.VoidedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"document_id":"{{command.DocumentId:D}}","document_type":"{{command.DocumentType}}","reason":{{(command.Reason is null ? "null" : "\"" + Escape(command.Reason) + "\"")}}}"""
            ),
            cancellationToken
        );
    }

    private async Task<ApprovalRequest> LoadPendingApprovalAsync(
        Guid documentId,
        CancellationToken cancellationToken
    )
    {
        // Load the most-recent ApprovalRequest for the document.
        // A document can have multiple historical requests across
        // submit / reject / re-submit cycles; the last one created
        // is the live Pending row.
        var request =
            await _db.Set<ApprovalRequest>()
                .OrderByDescending(r => r.SubmittedAtUtc)
                .FirstOrDefaultAsync(r => r.DocumentId == documentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No approval request found for document {documentId}; the document must be Submitted first."
            );

        if (request.Status != ApprovalRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Approval request {request.Id} for document {documentId} is already {request.Status}; cannot transition."
            );
        }

        return request;
    }

    /// <summary>
    /// Dispatches by document type. The three lambdas correspond to
    /// the three approval-eligible aggregates the MVP supports;
    /// adding a new doc type means adding one lambda + one switch
    /// arm.
    /// </summary>
    private async Task TransitionAsync(
        Guid documentId,
        DocumentType type,
        Action<SalesInvoice> sales,
        Action<PurchaseInvoice> purchase,
        Action<Expense> expense,
        CancellationToken cancellationToken
    )
    {
        switch (type)
        {
            case DocumentType.SalesInvoice:
            case DocumentType.CreditNote:
            {
                var doc =
                    await _db.Set<SalesInvoice>()
                        .FirstOrDefaultAsync(s => s.Id == documentId, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Sales invoice {documentId} not found."
                    );
                sales(doc);
                break;
            }
            case DocumentType.PurchaseInvoice:
            {
                var doc =
                    await _db.Set<PurchaseInvoice>()
                        .FirstOrDefaultAsync(p => p.Id == documentId, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Purchase invoice {documentId} not found."
                    );
                purchase(doc);
                break;
            }
            case DocumentType.Expense:
            {
                var doc =
                    await _db.Set<Expense>()
                        .FirstOrDefaultAsync(e => e.Id == documentId, cancellationToken)
                    ?? throw new InvalidOperationException($"Expense {documentId} not found.");
                expense(doc);
                break;
            }
            default:
                throw new InvalidOperationException(
                    $"Document type {type} is not yet wired into the approval workflow."
                );
        }
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
