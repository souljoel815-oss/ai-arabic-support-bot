using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Journals;

/// <summary>
/// FR-012 — operator-issued command to reverse a previously-posted
/// JournalVoucher. The reversal carries the SAME amounts with
/// debits ↔ credits flipped so the books wash to zero, and links
/// back to the original via <c>ReversesJournalVoucherId</c>.
///
/// FR-031 role gate applies (Administrator + Accountant only —
/// Bookkeeper rejected, same as manual adjustments). Two DB-side
/// integrity checks run in the handler:
///   * Original MUST exist.
///   * Original MUST NOT have been reversed before (one reversal
///     per original; a second reversal would produce two
///     compensating entries against the same source — chaos).
/// </summary>
public sealed record CreateReversalJournalCommand(
    Guid OriginalJournalVoucherId,
    DateOnly ReversalDate,
    ArabicEnglishText ReversalNarration,
    Guid CreatedByUserId);
