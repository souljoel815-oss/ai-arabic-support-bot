using EgyptTax.Domain.Workflow;

namespace EgyptTax.Application.Journals;

/// <summary>
/// FR-024 / FR-025 / T171 — single query surface for the
/// operator-facing journal ledger UI. Unifies two underlying
/// aggregates the operator shouldn't need to know about:
///   * <c>JournalEntry</c> (auto-emitted on Post — sales, purchase,
///     expense; T095 + T165 + this batch).
///   * <c>JournalVoucher</c> (operator-created — manual adjusting
///     vouchers per FR-031; reversal vouchers per FR-012).
/// Both surface in the same chronological list with a
/// <see cref="JournalListRow.Kind"/> column distinguishing source.
/// FR-025 drill-back lives in the detail view: auto-emitted entries
/// link to the originating sales / purchase / expense document;
/// reversal vouchers link back to the original JournalVoucher.
/// </summary>
public interface IJournalLedgerQuery
{
    Task<IReadOnlyList<JournalListRow>> ListAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default
    );

    Task<JournalDetailDto?> GetAsync(
        Guid id,
        JournalEntryKind kind,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Whether the row originated from a Post auto-emit or an operator-created voucher.</summary>
public enum JournalEntryKind
{
    AutoEmitted,
    ManualAdjusting,
    Reversal,
}

public sealed record JournalListRow(
    Guid Id,
    JournalEntryKind Kind,
    DateTime PostedAtUtc,
    string SourceLabel,
    DocumentType? SourceDocumentType,
    Guid? SourceDocumentId,
    string? SourceDocumentNumber,
    decimal TotalDebits,
    int LineCount
);

public sealed record JournalDetailDto(
    Guid Id,
    JournalEntryKind Kind,
    DateTime PostedAtUtc,
    string Narration,
    DocumentType? SourceDocumentType,
    Guid? SourceDocumentId,
    string? SourceDocumentNumber,
    Guid? ReversesJournalVoucherId,
    decimal TotalDebits,
    decimal TotalCredits,
    IReadOnlyList<JournalDetailLineDto> Lines
);

public sealed record JournalDetailLineDto(
    string AccountCode,
    decimal Debit,
    decimal Credit,
    string Description
);
