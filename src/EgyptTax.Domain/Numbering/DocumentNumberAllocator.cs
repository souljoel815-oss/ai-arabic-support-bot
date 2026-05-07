namespace EgyptTax.Domain.Numbering;

/// <summary>
/// FR-011 — the per-(series, fiscal year) counter row. Holds the
/// next-to-assign number; allocation reads + increments under
/// <c>UPDLOCK, HOLDLOCK</c> inside the same transaction as the post,
/// so a rolled-back post releases the consumed number.
/// </summary>
public sealed class DocumentNumberAllocator
{
    public Guid SeriesId { get; init; }
    public int FiscalYear { get; init; }
    public int NextNumber { get; private set; }

    private DocumentNumberAllocator() { }

    public DocumentNumberAllocator(Guid seriesId, int fiscalYear, int nextNumber)
    {
        SeriesId = seriesId;
        FiscalYear = fiscalYear;
        NextNumber = nextNumber;
    }

    public int Consume()
    {
        var assigned = NextNumber;
        NextNumber++;
        return assigned;
    }
}
