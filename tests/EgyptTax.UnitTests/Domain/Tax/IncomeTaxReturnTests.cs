using EgyptTax.Domain.Tax;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Domain.Tax;

/// <summary>
/// G3.4 — covers <see cref="IncomeTaxReturn"/> construction guards
/// + Generated → Submitted → Acknowledged lifecycle. Mirrors the
/// VAT-return tests since the lifecycle is identical; the
/// fiscal-year + regime fields are the differentiator.
/// </summary>
public class IncomeTaxReturnTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_DefaultsToGenerated()
    {
        var r = NewStandard();
        r.Status.Should().Be(IncomeTaxReturnStatus.Generated);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(10_000)]
    public void Constructor_RejectsBadFiscalYear(int year)
    {
        var act = () => Build(fiscalYear: year);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_RejectsInvertedPeriod()
    {
        var act = () => Build(
            start: new DateOnly(2026, 12, 31),
            end: new DateOnly(2026, 1, 1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_RejectsEmptyUser()
    {
        var act = () => Build(generatedBy: Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Constructor_RejectsOutOfRangeEffectiveRate(double rate)
    {
        var act = () => Build(effectiveRate: (decimal)rate);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkSubmitted_FlipsStatus_StoresReference()
    {
        var r = NewStandard();
        r.MarkSubmitted("ETA-IT-2026-ABC", User, T0);

        r.Status.Should().Be(IncomeTaxReturnStatus.Submitted);
        r.RegulatorSubmissionReference.Should().Be("ETA-IT-2026-ABC");
        r.SubmittedAtUtc.Should().Be(T0);
        r.SubmittedByUserId.Should().Be(User);
    }

    [Fact]
    public void MarkSubmitted_IsIdempotentForSameReference()
    {
        var r = NewStandard();
        r.MarkSubmitted("ETA-IT-2026-ABC", User, T0);
        r.MarkSubmitted("ETA-IT-2026-ABC", User, T0.AddDays(1));

        r.SubmittedAtUtc.Should().Be(T0); // first wins
    }

    [Fact]
    public void MarkSubmitted_RejectsDifferentReference()
    {
        var r = NewStandard();
        r.MarkSubmitted("ETA-IT-2026-ABC", User, T0);
        var act = () => r.MarkSubmitted("ETA-IT-2026-XYZ", User, T0.AddDays(1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already submitted*");
    }

    [Fact]
    public void MarkSubmitted_RejectsAfterAcknowledged()
    {
        var r = NewStandard();
        r.MarkSubmitted("ETA-IT-2026-ABC", User, T0);
        r.MarkAcknowledged("ETA-IT-2026-OK", User, T0.AddDays(2));

        var act = () => r.MarkSubmitted("anything", User, T0.AddDays(3));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already acknowledged*");
    }

    [Fact]
    public void MarkAcknowledged_RejectsBeforeSubmission()
    {
        var r = NewStandard();
        var act = () => r.MarkAcknowledged("OK", User, T0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*hasn't been submitted*");
    }

    [Fact]
    public void MarkAcknowledged_FlipsStatus_AndIsIdempotent()
    {
        var r = NewStandard();
        r.MarkSubmitted("ETA-IT-2026-ABC", User, T0);
        r.MarkAcknowledged("ETA-IT-2026-OK", User, T0.AddDays(2));
        r.MarkAcknowledged("ETA-IT-2026-OK", User, T0.AddDays(3));

        r.Status.Should().Be(IncomeTaxReturnStatus.Acknowledged);
        r.AcknowledgedAtUtc.Should().Be(T0.AddDays(2)); // first wins
    }

    [Fact]
    public void UpdateNote_TrimsAndNullsBlank()
    {
        var r = NewStandard();
        r.UpdateNote("   ");
        r.Note.Should().BeNull();

        r.UpdateNote("  hello  ");
        r.Note.Should().Be("hello");
    }

    private static IncomeTaxReturn NewStandard() => Build();

    private static IncomeTaxReturn Build(
        IncomeTaxRegime regime = IncomeTaxRegime.Standard,
        int fiscalYear = 2026,
        DateOnly? start = null,
        DateOnly? end = null,
        decimal effectiveRate = 18.4m,
        Guid? generatedBy = null) =>
        new(
            regime: regime,
            fiscalYear: fiscalYear,
            periodStart: start ?? new DateOnly(2026, 1, 1),
            periodEnd: end ?? new DateOnly(2026, 12, 31),
            revenue: MoneyEgp.From(1_000_000m),
            deductibleExpenses: MoneyEgp.From(400_000m),
            nonDeductibleAdjustments: MoneyEgp.From(50_000m),
            managementProfitLoss: MoneyEgp.From(550_000m),
            taxableIncome: MoneyEgp.From(600_000m),
            taxDue: MoneyEgp.From(110_000m),
            effectiveRatePercent: effectiveRate,
            contributingDocumentCount: 73,
            generatedAtUtc: T0,
            generatedByUserId: generatedBy ?? User);
}
