using EgyptTax.Application.Compliance;
using EgyptTax.Application.Periods;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Periods;

public class PeriodLockReadinessGateTests
{
    [Fact]
    public void CleanCockpit_AllowsLock_WithoutForce()
    {
        var cockpit = NewCockpit();

        var decision = PeriodLockReadinessGate.Evaluate(cockpit, forceRequested: false);

        decision.Allowed.Should().BeTrue();
        decision.RequiresForce.Should().BeFalse();
        decision.HardBlockers.Should().BeEmpty();
        decision.SoftBlockers.Should().BeEmpty();
    }

    [Fact]
    public void DraftsInPeriod_HardBlocks_EvenWithForce()
    {
        var cockpit = NewCockpit(draftsInPeriod: 3);

        var decision = PeriodLockReadinessGate.Evaluate(cockpit, forceRequested: true);

        decision.Allowed.Should().BeFalse();
        decision.HardBlockers.Should().ContainSingle(b => b.Code == "DRAFTS_IN_PERIOD");
    }

    [Fact]
    public void FailedEtaSubmissions_HardBlock_EvenWithForce()
    {
        var cockpit = NewCockpit(failedEtaSubmissions: 2);

        var decision = PeriodLockReadinessGate.Evaluate(cockpit, forceRequested: true);

        decision.Allowed.Should().BeFalse();
        decision.HardBlockers.Should().ContainSingle(b => b.Code == "FAILED_ETA_SUBMISSIONS");
    }

    [Fact]
    public void MissingDocuments_AreSoftBlockers_PassWithForce()
    {
        var cockpit = NewCockpit(missingBuckets:
            new[]
            {
                new MissingDocumentBucket(
                    "Deductible posts without attachments",
                    Count: 5,
                    Examples: Array.Empty<MissingDocumentExample>()),
            });

        var noForce = PeriodLockReadinessGate.Evaluate(cockpit, forceRequested: false);
        noForce.Allowed.Should().BeFalse();
        noForce.RequiresForce.Should().BeTrue();
        noForce.SoftBlockers.Should().ContainSingle();

        var withForce = PeriodLockReadinessGate.Evaluate(cockpit, forceRequested: true);
        withForce.Allowed.Should().BeTrue();
        withForce.RequiresForce.Should().BeTrue();
    }

    [Fact]
    public void HardAndSoftBoth_HardWinsRegardlessOfForce()
    {
        var cockpit = NewCockpit(
            draftsInPeriod: 1,
            missingBuckets: new[]
            {
                new MissingDocumentBucket(
                    "Sales without ETA",
                    Count: 2,
                    Examples: Array.Empty<MissingDocumentExample>()),
            });

        var decision = PeriodLockReadinessGate.Evaluate(cockpit, forceRequested: true);

        decision.Allowed.Should().BeFalse();
        decision.HardBlockers.Should().NotBeEmpty();
    }

    private static MonthlyTaxClosingCockpit NewCockpit(
        int draftsInPeriod = 0,
        int failedEtaSubmissions = 0,
        IReadOnlyList<MissingDocumentBucket>? missingBuckets = null)
    {
        return new MonthlyTaxClosingCockpit(
            Year: 2026,
            Month: 5,
            PeriodStart: new DateOnly(2026, 5, 1),
            PeriodEnd: new DateOnly(2026, 5, 31),
            PeriodIsLocked: false,
            VatReadinessPercent: 100m,
            TotalPostsInPeriod: 0,
            CleanPostsCount: 0,
            MissingDocuments: missingBuckets ?? Array.Empty<MissingDocumentBucket>(),
            FailedEtaSubmissionCount: failedEtaSubmissions,
            FailedEtaSubmissionTotalGrand: MoneyEgp.From(0m),
            DraftsInPeriodCount: draftsInPeriod,
            NonRecoverableInputVat: MoneyEgp.From(0m),
            PeriodLockChecklist: Array.Empty<PeriodLockChecklistItem>());
    }
}
