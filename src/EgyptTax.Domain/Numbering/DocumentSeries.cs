using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Numbering;

/// <summary>
/// FR-011 — a configurable numbering series. The MVP seeds one
/// series per <see cref="DocumentType"/> at install time (e.g.
/// <c>INV</c> for sales invoices, <c>PI</c> for purchase invoices).
/// The <see cref="Code"/> is what gets embedded in the canonical
/// document-number format <c>&lt;CODE&gt;-&lt;YYYY&gt;-&lt;NNNNNN&gt;</c>.
/// </summary>
public sealed class DocumentSeries
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public DocumentType DocumentType { get; init; }

    private DocumentSeries() { }

    public DocumentSeries(string code, ArabicEnglishText name, DocumentType documentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Name = name;
        DocumentType = documentType;
    }

    public DocumentSeries(Guid id, string code, ArabicEnglishText name, DocumentType documentType)
        : this(code, name, documentType)
    {
        Id = id;
    }
}
