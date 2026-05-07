using EgyptTax.Domain.Workflow;

namespace EgyptTax.Application.Numbering;

/// <summary>
/// FR-011 — port for the gap-free numbering allocator. The
/// Infrastructure implementation (<c>SqlSequentialNumberAllocator</c>)
/// runs inside the same transaction as the post operation; rollback
/// releases the consumed number per the FR-011 invariant. Produces
/// the canonical <c>&lt;CODE&gt;-&lt;YYYY&gt;-&lt;NNNNNN&gt;</c> format
/// that gets embedded in the PDF, the eInvoice JSON, and the
/// inspection bundle.
/// </summary>
public interface IDocumentNumberAllocator
{
    Task<string> AllocateAsync(DocumentType type, int fiscalYear, CancellationToken cancellationToken = default);
}
