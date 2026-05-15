using EgyptTax.Domain.Workflow;

namespace EgyptTax.Domain.Expenses;

/// <summary>
/// v5 B.4 — bundle of N <see cref="Expense"/> rows submitted for
/// approval as a single unit. The use case: an employee returns
/// from a week of conference travel with 12 receipts; instead of
/// Submit/Approve on each individually, they create one report
/// ("Conference – March 2026"), tick the 12 expenses, and the
/// manager approves the whole bundle.
///
/// State machine reuses the canonical <see cref="DocumentState"/>
/// (Draft → Submitted → Approved). On Submit the children move
/// Draft → Submitted in the same transaction; on Approve they all
/// move Submitted → Approved; on Reject they all bounce back to
/// Draft. Cascade lives in <c>DocumentApprovalHandler</c> so it
/// runs inside the existing one-SaveChanges-per-transition envelope.
///
/// TotalAmount is intentionally NOT stored on the entity — sum the
/// children at query time. Storing it would require a recompute
/// every time an Expense.Amount changed, and Expenses are mutable
/// in Draft. The list/detail pages do the sum (≤200 children per
/// report in any realistic scenario).
/// </summary>
public sealed class ExpenseReport
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Operator-supplied label, e.g. "Travel — Cairo conf — Mar 2026".</summary>
    public string Name { get; private set; } = default!;

    public Guid CreatedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    public DocumentState State { get; private set; } = DocumentState.Draft;

    private ExpenseReport() { }

    public ExpenseReport(string name, Guid createdByUserId, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("CreatedByUserId required.", nameof(createdByUserId));
        }
        Name = name.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public void Rename(string name)
    {
        ThrowIfNotDraft(nameof(Rename));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void MarkSubmitted() =>
        State = DocumentStateMachine.Transition(State, DocumentState.Submitted, approvalEnabled: true);

    public void MarkApproved() =>
        State = DocumentStateMachine.Transition(State, DocumentState.Approved, approvalEnabled: true);

    /// <summary>Reject sends the report back to Draft so the
    /// employee can edit / drop expenses and re-submit.</summary>
    public void MarkRejected() =>
        State = DocumentStateMachine.Transition(State, DocumentState.Draft, approvalEnabled: true);

    private void ThrowIfNotDraft(string operation)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} on expense report {Id}: current state {State} is not Draft.");
        }
    }
}
