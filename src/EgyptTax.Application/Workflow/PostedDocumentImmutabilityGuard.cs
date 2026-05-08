using EgyptTax.Domain.Workflow;

namespace EgyptTax.Application.Workflow;

/// <summary>
/// FR-012 / FR-027 / INV-002 — handler-level guard that rejects any
/// modification, deletion, or void of a Posted document. The
/// rejection message surfaces the correct correction path: a
/// credit note (FR-013) for tax-impacting documents, or a reversal
/// voucher for non-tax-impacting documents (manual journals,
/// payment vouchers).
/// </summary>
public static class PostedDocumentImmutabilityGuard
{
    public static void EnsureNotPosted(
        DocumentState state,
        DocumentType documentType,
        Guid documentId,
        string operation
    )
    {
        if (state != DocumentState.Posted)
        {
            return;
        }

        var hint = documentType.IsTaxImpacting()
            ? "issue a credit note (FR-013) instead"
            : "create a reversal voucher referencing the original instead";

        throw new InvalidOperationException(
            $"Cannot {operation} {documentType} {documentId}: the document is Posted and immutable. "
                + $"To correct it, {hint}."
        );
    }
}
