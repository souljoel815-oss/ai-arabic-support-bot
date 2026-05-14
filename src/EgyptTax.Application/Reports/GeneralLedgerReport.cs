using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Reports;

/// <summary>
/// v4 A.1 — General Ledger. The Trial Balance with the per-account
/// drilldown expanded: every <c>JournalEntryLine</c> that hit each
/// account in the period, in chronological order, with a running
/// balance. This is the report accountants pull at month-end to
/// answer "show me everything that touched account 1200 (AR) this
/// month and the running balance after each transaction."
///
/// Mirrors the data source the Trial Balance uses
/// (<c>JournalEntry</c> only — manual <c>JournalVoucher</c> rows
/// don't yet feed the books). Sign convention: running balance is
/// debit-positive (<c>debit − credit</c>), matching Trial Balance's
/// <c>NetBalance</c> — accountants flip signs mentally per account
/// class.
/// </summary>
public sealed record GeneralLedgerReport(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<GeneralLedgerSection> Sections,
    MoneyEgp TotalDebits,
    MoneyEgp TotalCredits
);

/// <summary>One per account that has either an opening balance OR
/// activity in the period.</summary>
public sealed record GeneralLedgerSection(
    string AccountCode,
    MoneyEgp OpeningBalance,
    IReadOnlyList<GeneralLedgerEntry> Entries,
    MoneyEgp PeriodDebitTotal,
    MoneyEgp PeriodCreditTotal,
    MoneyEgp ClosingBalance
);

/// <summary>One per <c>JournalEntryLine</c> in the period for the
/// account. The <see cref="SourceDocumentId"/> + <see cref="SourceDocumentType"/>
/// pair lets the page emit a click-through link to the originating
/// invoice / expense / fixed-asset.</summary>
public sealed record GeneralLedgerEntry(
    DateTime PostedAtUtc,
    Guid SourceDocumentId,
    string SourceDocumentNumber,
    DocumentType SourceDocumentType,
    string Description,
    MoneyEgp Debit,
    MoneyEgp Credit,
    MoneyEgp RunningBalance
);
