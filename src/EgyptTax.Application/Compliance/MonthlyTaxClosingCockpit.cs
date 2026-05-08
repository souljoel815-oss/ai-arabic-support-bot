using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance;

/// <summary>
/// Differentiator 2 — Monthly Tax Closing Cockpit. The single
/// landing surface used during the month-close ritual. Pulls
/// together the answers to "am I ready to lock and file May?":
///  * <see cref="VatReadiness"/> — % of period's tax-impacting
///    posts with no MustFixBeforeFiling or Blocker findings.
///  * <see cref="MissingDocuments"/> — deductible posts without
///    attachments, posted sales invoices without ETA submissions,
///    etc. (per-bucket counts + first 10 examples each).
///  * <see cref="FailedEtaSubmissions"/> — count + sum of grand
///    totals.
///  * <see cref="DraftsInPeriod"/> — drafts dated in the period
///    that need to post before the operator can lock.
///  * <see cref="NonRecoverableInputVat"/> — sum of input VAT on
///    posted purchase invoices from non-RegisteredTaxpayer suppliers
///    (FR-020 information; not a blocker but useful for variance
///    against VAT report).
///  * <see cref="PeriodLockChecklist"/> — flat list of (item,
///    cleared) pairs the cockpit shows under "what must clear
///    before I can lock". Same items the spec §692 calls out.
///
/// Queries are EF-backed for now (R-02 says reports MAY use Dapper
/// for hot paths; the SC-002 < 5 s p95 at 5 k docs bar is hit by
/// EF + the existing FK indexes; swap to Dapper when profiling
/// demands). T236a's IMemoryCache decorator + invalidation hook
/// is deferred until invalidation events are surfaced from the
/// post handlers.
/// </summary>
public sealed record MonthlyTaxClosingCockpit(
    int Year,
    int Month,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    bool PeriodIsLocked,
    decimal VatReadinessPercent,
    int TotalPostsInPeriod,
    int CleanPostsCount,
    IReadOnlyList<MissingDocumentBucket> MissingDocuments,
    int FailedEtaSubmissionCount,
    MoneyEgp FailedEtaSubmissionTotalGrand,
    int DraftsInPeriodCount,
    MoneyEgp NonRecoverableInputVat,
    IReadOnlyList<PeriodLockChecklistItem> PeriodLockChecklist
);

/// <summary>
/// One named bucket of "missing-something" documents. Counts +
/// first few examples (with their drill-down ids so the cockpit
/// page can link directly to fix them).
/// </summary>
public sealed record MissingDocumentBucket(
    string Name,
    int Count,
    IReadOnlyList<MissingDocumentExample> Examples
);

public sealed record MissingDocumentExample(
    Guid DocumentId,
    string Kind,
    string? DocumentNumber,
    DateOnly DocumentDate,
    string Description
);

/// <summary>
/// One row in the "what must clear before I can lock" checklist.
/// <see cref="Cleared"/>=true means the item is done; the cockpit
/// can show ✓/✗ inline.
/// </summary>
public sealed record PeriodLockChecklistItem(
    string Description,
    bool Cleared,
    int? RelatedCount = null
);
