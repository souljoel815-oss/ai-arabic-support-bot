using EgyptTax.Application.Compliance;
using EgyptTax.SharedKernel;

namespace EgyptTax.IntegrationTests.Infrastructure;

/// <summary>
/// Test stub for <see cref="IMonthlyTaxClosingCockpitQuery"/>:
/// returns a fully-clean cockpit (no drafts, no failed ETA, no
/// missing buckets, empty checklist) so the period-lock gate
/// always passes. Used by tests that exercise the lock /
/// reopen mechanic and don't care about the cockpit gate
/// itself; the gate has its own unit-test coverage.
/// </summary>
internal sealed class AlwaysCleanCockpitQuery : IMonthlyTaxClosingCockpitQuery
{
    public Task<MonthlyTaxClosingCockpit> RunAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MonthlyTaxClosingCockpit(
            Year: year,
            Month: month,
            PeriodStart: new DateOnly(year, month, 1),
            PeriodEnd: new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            PeriodIsLocked: false,
            VatReadinessPercent: 100m,
            TotalPostsInPeriod: 0,
            CleanPostsCount: 0,
            MissingDocuments: Array.Empty<MissingDocumentBucket>(),
            FailedEtaSubmissionCount: 0,
            FailedEtaSubmissionTotalGrand: MoneyEgp.From(0m),
            DraftsInPeriodCount: 0,
            NonRecoverableInputVat: MoneyEgp.From(0m),
            PeriodLockChecklist: Array.Empty<PeriodLockChecklistItem>()));
    }
}
