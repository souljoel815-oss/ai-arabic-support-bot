using EgyptTax.Domain.Periods;

namespace EgyptTax.Application.Periods;

/// <summary>
/// FR-037 — close a tax period for filing. Administrator-only.
/// MonthOrQuarter is interpreted per <see cref="PeriodKind"/>: 1-12
/// for VatMonth, 1-4 for WhtQuarter, 0 for IncomeFiscalYear.
/// </summary>
public sealed record LockTaxPeriodCommand(
    TaxPeriodKind PeriodKind,
    int Year,
    int MonthOrQuarter,
    Guid LockedByUserId,
    string? Reason,
    /// <summary>
    /// P3.6 — non-null + non-blank means "I see soft blockers and
    /// I'm overriding them anyway". Hard blockers are never
    /// bypassed regardless of this value. Recorded verbatim in the
    /// <c>tax_period.locked.forced</c> audit row so the auditor can
    /// see why the operator chose to lock past the warnings.
    /// </summary>
    string? ForceLockReason = null
);

/// <summary>FR-037 — Administrator-only reopen.</summary>
public sealed record ReopenTaxPeriodCommand(
    TaxPeriodKind PeriodKind,
    int Year,
    int MonthOrQuarter,
    Guid ReopenedByUserId,
    string? Reason
);
