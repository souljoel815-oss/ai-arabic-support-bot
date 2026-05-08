using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Journals;

/// <summary>
/// FR-031 — operator-issued command to create a manual adjusting
/// journal voucher. The handler enforces both invariants:
///   * Caller MUST hold a role that includes the manual-journal
///     permission (Administrator + Accountant — Bookkeeper rejected).
///   * Lines MUST balance (SUM debits = SUM credits) — enforced
///     inside <see cref="EgyptTax.Domain.Documents.JournalVoucher"/>'s
///     factory so an unbalanced caller never produces a voucher
///     instance.
/// </summary>
public sealed record CreateManualAdjustingJournalCommand(
    DateOnly Date,
    ArabicEnglishText Narration,
    Guid CreatedByUserId,
    IReadOnlyList<ManualJournalLineInput> Lines);

public sealed record ManualJournalLineInput(
    string AccountCode,
    MoneyEgp Debit,
    MoneyEgp Credit,
    string Description);
