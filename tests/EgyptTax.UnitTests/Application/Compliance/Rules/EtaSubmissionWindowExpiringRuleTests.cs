using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

public class EtaSubmissionWindowExpiringRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Silent_When_No_EtaSubmission()
    {
        new EtaSubmissionWindowExpiringRule()
            .Evaluate(BuildContext(submission: null))
            .Should()
            .BeEmpty(
                because: "draft invoices never have an ETA submission row — the rule has nothing to evaluate"
            );
    }

    [Fact]
    public void Silent_When_Submission_Already_Submitted()
    {
        var sub = BuildSubmission(EtaSubmissionStatus.Submitted, hoursToDeadline: 1);
        new EtaSubmissionWindowExpiringRule()
            .Evaluate(BuildContext(sub))
            .Should()
            .BeEmpty(
                because: "Submitted is the terminal-success state — the regulator already accepted it"
            );
    }

    [Fact]
    public void Warning_When_Pending_And_24h_Or_Less_Remain()
    {
        var sub = BuildSubmission(EtaSubmissionStatus.Pending, hoursToDeadline: 18);
        var findings = new EtaSubmissionWindowExpiringRule().Evaluate(BuildContext(sub));
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.Warning);
    }

    [Fact]
    public void MustFix_When_Less_Than_6h_Remain()
    {
        var sub = BuildSubmission(EtaSubmissionStatus.Failed, hoursToDeadline: 3);
        var findings = new EtaSubmissionWindowExpiringRule().Evaluate(BuildContext(sub));
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.MustFixBeforeFiling);
    }

    [Fact]
    public void Blocker_When_Window_Already_Expired()
    {
        var sub = BuildSubmission(EtaSubmissionStatus.Failed, hoursToDeadline: -2);
        var findings = new EtaSubmissionWindowExpiringRule().Evaluate(BuildContext(sub));
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.Blocker);
    }

    [Fact]
    public void Silent_When_Window_More_Than_24h_Out()
    {
        var sub = BuildSubmission(EtaSubmissionStatus.Pending, hoursToDeadline: 72);
        new EtaSubmissionWindowExpiringRule()
            .Evaluate(BuildContext(sub))
            .Should()
            .BeEmpty(because: "with > 24h on the clock there's nothing yet to escalate");
    }

    private static EtaSubmission BuildSubmission(EtaSubmissionStatus status, double hoursToDeadline)
    {
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);
        var postedAt =
            nowUtc
            - TimeSpan.FromDays(EtaSubmission.DefaultSubmissionWindowDays)
            + TimeSpan.FromHours(hoursToDeadline);
        var sub = new EtaSubmission(Guid.NewGuid(), postedAt, postedAt);
        if (status != EtaSubmissionStatus.Pending)
        {
            sub.RecordAttempt(
                status,
                submissionUuid: null,
                errorCode: status == EtaSubmissionStatus.Failed ? "MOCK" : null,
                errorMessage: status == EtaSubmissionStatus.Failed ? "mock failure" : null,
                nowUtc: nowUtc
            );
        }
        return sub;
    }

    private static DocumentRiskContext BuildContext(EtaSubmission? submission)
    {
        var snapshot = CustomerTaxProfile.B2BRegistered(
            EgyptianTin.Parse("123456789"),
            false,
            VatId
        );
        var invoice = SalesInvoice.CreateDraft(Guid.NewGuid(), snapshot, new DateOnly(2026, 5, 7));
        invoice.AddLine(Guid.NewGuid(), 1m, MoneyEgp.From(100m), VatId, 14m);
        return new DocumentRiskContext(
            invoice,
            new Dictionary<Guid, Item>(),
            submission,
            new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc)
        );
    }
}
