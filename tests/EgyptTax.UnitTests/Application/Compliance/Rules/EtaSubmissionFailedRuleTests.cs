using EgyptTax.Application.Compliance.RiskScoring;
using EgyptTax.Application.Compliance.RiskScoring.Rules;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Application.Compliance.Rules;

public class EtaSubmissionFailedRuleTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void Silent_When_No_Submission()
    {
        new EtaSubmissionFailedRule().Evaluate(BuildContext(null))
            .Should().BeEmpty();
    }

    [Fact]
    public void Silent_When_Pending()
    {
        var sub = NewPending();
        new EtaSubmissionFailedRule().Evaluate(BuildContext(sub))
            .Should().BeEmpty(because: "Pending with zero attempts has nothing to flag yet");
    }

    [Fact]
    public void Silent_When_Submitted()
    {
        var sub = NewPending();
        sub.RecordAttempt(EtaSubmissionStatus.Submitted,
            submissionUuid: "etasub-123", errorCode: null, errorMessage: null,
            nowUtc: DateTime.UtcNow);
        new EtaSubmissionFailedRule().Evaluate(BuildContext(sub))
            .Should().BeEmpty();
    }

    [Fact]
    public void Warning_For_Single_Failed_Attempt()
    {
        var sub = NewPending();
        sub.RecordAttempt(EtaSubmissionStatus.Failed, null, "ETA_TIMEOUT", "timeout", DateTime.UtcNow);

        var findings = new EtaSubmissionFailedRule().Evaluate(BuildContext(sub));
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.Warning);
        findings[0].RuleId.Should().Be("ETA.SUBMISSION_FAILED");
    }

    [Fact]
    public void MustFix_When_Three_Or_More_Failed_Attempts()
    {
        var sub = NewPending();
        for (var i = 0; i < 3; i++)
        {
            sub.RecordAttempt(EtaSubmissionStatus.Failed, null, "ETA_TIMEOUT", "timeout", DateTime.UtcNow);
        }

        var findings = new EtaSubmissionFailedRule().Evaluate(BuildContext(sub));
        findings.Should().HaveCount(1);
        findings[0].Severity.Should().Be(RiskSeverity.MustFixBeforeFiling,
            because: "repeated failures are unlikely to clear on the next retry tick — operator must look");
    }

    private static EtaSubmission NewPending()
    {
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);
        return new EtaSubmission(Guid.NewGuid(), nowUtc, nowUtc);
    }

    private static DocumentRiskContext BuildContext(EtaSubmission? submission)
    {
        var snapshot = CustomerTaxProfile.B2BRegistered(
            EgyptianTin.Parse("123456789"), false, VatId);
        var invoice = SalesInvoice.CreateDraft(Guid.NewGuid(), snapshot, new DateOnly(2026, 5, 7));
        invoice.AddLine(Guid.NewGuid(), 1m, MoneyEgp.From(100m), VatId, 14m);
        return new DocumentRiskContext(invoice, new Dictionary<Guid, Item>(), submission,
            new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));
    }
}
