namespace EgyptTax.Domain.Workflow;

/// <summary>
/// FR-026 — the document state machine. Encodes the transition graph
/// shared by every document aggregate. Approval-enabled types follow
/// Draft → Submitted → Approved → Posted; approval-disabled types
/// allow Draft → Posted directly. Voiding is permitted only from
/// non-Posted states (FR-027). Posted is fully terminal — corrections
/// route through FR-013 credit notes (tax-impacting docs) or reversal
/// vouchers (non-tax-impacting docs); the rejection of edit/delete/void
/// on Posted is the responsibility of <c>PostedDocumentImmutabilityGuard</c>.
/// </summary>
public static class DocumentStateMachine
{
    /// <summary>
    /// Returns true when the document may transition from
    /// <paramref name="from"/> to <paramref name="to"/>. Per
    /// <paramref name="approvalEnabled"/>, the direct Draft → Posted
    /// path opens up when approval is not required for the document
    /// type.
    /// </summary>
    public static bool CanTransition(DocumentState from, DocumentState to, bool approvalEnabled)
    {
        // Posted and Voided are terminal regardless of approval config.
        if (from is DocumentState.Posted or DocumentState.Voided)
        {
            return false;
        }

        return (from, to) switch
        {
            // Universal transitions (apply whether or not approval is enabled).
            (DocumentState.Draft, DocumentState.Voided) => true,
            (DocumentState.Submitted, DocumentState.Voided) => true,
            (DocumentState.Approved, DocumentState.Voided) => true,

            // Approval-enabled lifecycle.
            (DocumentState.Draft, DocumentState.Submitted) => true,
            (DocumentState.Submitted, DocumentState.Approved) => true,
            (DocumentState.Submitted, DocumentState.Draft) => true, // reject returns to Draft
            (DocumentState.Approved, DocumentState.Posted) => true,

            // Direct-post lifecycle: only when approval is NOT required.
            (DocumentState.Draft, DocumentState.Posted) => !approvalEnabled,

            _ => false,
        };
    }

    /// <summary>
    /// Performs the transition or throws if it is not allowed. Returns
    /// <paramref name="to"/> on success so call-sites can chain.
    /// </summary>
    public static DocumentState Transition(
        DocumentState from,
        DocumentState to,
        bool approvalEnabled
    )
    {
        if (!CanTransition(from, to, approvalEnabled))
        {
            throw new InvalidOperationException(
                $"Document state transition from {from} to {to} is not allowed (approvalEnabled={approvalEnabled})."
            );
        }
        return to;
    }
}
