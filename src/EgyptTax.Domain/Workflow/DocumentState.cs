namespace EgyptTax.Domain.Workflow;

/// <summary>
/// FR-026 — the canonical document lifecycle states. <see cref="Posted"/>
/// and <see cref="Voided"/> are terminal: once entered, no further
/// transitions are allowed (corrections to posted tax-impacting
/// documents flow through credit notes per FR-013, and posted
/// non-tax-impacting documents are corrected via reversal vouchers).
/// </summary>
public enum DocumentState
{
    Draft,
    Submitted,
    Approved,
    Posted,
    Voided,
}
