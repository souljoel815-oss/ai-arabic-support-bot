using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Accounting;

/// <summary>
/// T095 — a posted journal entry: the double-entry record emitted
/// when a tax-impacting source document (currently SalesInvoice +
/// CreditNote) is posted. Each entry references the source document
/// by id + canonical document number + type, so the GL printout
/// drills back to the originating invoice. The full
/// <c>JournalVoucher</c> aggregate (manual entries, narration,
/// reversal links) lands in US4; this lightweight entity ships now
/// so US1 has balanced books from day one and the SC-013 trial
/// balance has rows to read.
///
/// Invariant: SUM(line.Debit) == SUM(line.Credit). The factory
/// <see cref="Create"/> validates this before construction and
/// throws if the caller supplies an unbalanced set.
/// </summary>
public sealed class JournalEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SourceDocumentId { get; init; }
    public string SourceDocumentNumber { get; init; } = "";
    public DocumentType SourceDocumentType { get; init; }
    public DateTime PostedAtUtc { get; init; }

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyCollection<JournalEntryLine> Lines => _lines;

    private JournalEntry() { }

    private JournalEntry(
        Guid sourceDocumentId,
        string sourceDocumentNumber,
        DocumentType sourceDocumentType,
        DateTime postedAtUtc)
    {
        SourceDocumentId = sourceDocumentId;
        SourceDocumentNumber = sourceDocumentNumber;
        SourceDocumentType = sourceDocumentType;
        PostedAtUtc = postedAtUtc;
    }

    /// <summary>
    /// Build a JournalEntry from a caller-supplied list of one-side
    /// (debit XOR credit) tuples. The balance invariant is asserted
    /// here so an unbalanced emit never reaches the database.
    /// </summary>
    public static JournalEntry Create(
        Guid sourceDocumentId,
        string sourceDocumentNumber,
        DocumentType sourceDocumentType,
        DateTime postedAtUtc,
        IEnumerable<(string AccountCode, MoneyEgp Debit, MoneyEgp Credit, string Description)> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDocumentNumber);
        ArgumentNullException.ThrowIfNull(lines);

        var entry = new JournalEntry(
            sourceDocumentId, sourceDocumentNumber, sourceDocumentType, postedAtUtc);

        foreach (var l in lines)
        {
            entry._lines.Add(new JournalEntryLine(
                entry.Id, l.AccountCode, l.Debit, l.Credit, l.Description));
        }

        if (entry._lines.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot create a journal entry for {sourceDocumentNumber}: no lines were supplied.");
        }

        var sumDebits = entry._lines.Sum(x => x.Debit.Amount);
        var sumCredits = entry._lines.Sum(x => x.Credit.Amount);
        if (sumDebits != sumCredits)
        {
            throw new InvalidOperationException(
                $"Journal entry for {sourceDocumentNumber} is unbalanced: debits {sumDebits:F2} vs credits {sumCredits:F2}. Double-entry requires equality.");
        }

        return entry;
    }
}
