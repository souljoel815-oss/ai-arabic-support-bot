using EgyptTax.Domain.Tax;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Domain.Tax;

/// <summary>
/// G3.3 — covers <see cref="VatReturn"/> construction guards +
/// Generated → Submitted → Acknowledged lifecycle, including the
/// "no different submission reference for an already-submitted
/// return" rule that catches accidental double-submits.
/// </summary>
public class VatReturnTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_DefaultsToGenerated()
    {
        var r = NewMonthly();
        r.Status.Should().Be(VatReturnStatus.Generated);
    }

    [Theory]
    [InlineData(VatReturnPeriodKind.VatMonthly, 0)]
    [InlineData(VatReturnPeriodKind.VatMonthly, 13)]
    [InlineData(VatReturnPeriodKind.VatQuarterly, 0)]
    [InlineData(VatReturnPeriodKind.VatQuarterly, 5)]
    public void Constructor_RejectsInvalidOrdinal(VatReturnPeriodKind kind, int ordinal)
    {
        var act = () => new VatReturn(
            periodKind: kind,
            periodYear: 2026,
            periodOrdinal: ordinal,
            periodStart: new DateOnly(2026, 1, 1),
            periodEnd: new DateOnly(2026, 1, 31),
            outputVat: MoneyEgp.Zero,
            inputVatRecoverable: MoneyEgp.Zero,
            inputVatNonRecoverable: MoneyEgp.Zero,
            netPayable: MoneyEgp.Zero,
            contributingDocumentCount: 0,
            generatedAtUtc: T0,
            generatedByUserId: User);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkSubmitted_FlipsStatus_AndStoresReference()
    {
        var r = NewMonthly();
        r.MarkSubmitted("ETA-VAT-001", User, T0.AddHours(1));

        r.Status.Should().Be(VatReturnStatus.Submitted);
        r.RegulatorSubmissionReference.Should().Be("ETA-VAT-001");
        r.SubmittedAtUtc.Should().Be(T0.AddHours(1));
        r.SubmittedByUserId.Should().Be(User);
    }

    [Fact]
    public void MarkSubmitted_Idempotent_SameReference()
    {
        var r = NewMonthly();
        r.MarkSubmitted("ETA-VAT-001", User, T0);
        // Same reference, later timestamp — no-op.
        r.MarkSubmitted("ETA-VAT-001", User, T0.AddDays(1));
        r.SubmittedAtUtc.Should().Be(T0); // preserved
    }

    [Fact]
    public void MarkSubmitted_DifferentReference_Throws()
    {
        var r = NewMonthly();
        r.MarkSubmitted("ETA-VAT-001", User, T0);

        var act = () => r.MarkSubmitted("ETA-VAT-002", User, T0.AddHours(1));
        act.Should().Throw<InvalidOperationException>().WithMessage("*already submitted*");
    }

    [Fact]
    public void MarkAcknowledged_RequiresSubmittedFirst()
    {
        var r = NewMonthly();
        var act = () => r.MarkAcknowledged("ETA-ACK-001", User, T0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*hasn't been submitted*");
    }

    [Fact]
    public void MarkAcknowledged_AfterSubmitted_Succeeds()
    {
        var r = NewMonthly();
        r.MarkSubmitted("ETA-VAT-001", User, T0);
        r.MarkAcknowledged("ETA-ACK-001", User, T0.AddHours(2));

        r.Status.Should().Be(VatReturnStatus.Acknowledged);
        r.RegulatorAcknowledgementReference.Should().Be("ETA-ACK-001");
        r.AcknowledgedAtUtc.Should().Be(T0.AddHours(2));
    }

    [Fact]
    public void MarkAcknowledged_Idempotent()
    {
        var r = NewMonthly();
        r.MarkSubmitted("ETA-VAT-001", User, T0);
        r.MarkAcknowledged("ETA-ACK-001", User, T0.AddHours(2));
        // Second call — no exception, no overwrite.
        r.MarkAcknowledged("ETA-ACK-002", User, T0.AddHours(3));
        r.RegulatorAcknowledgementReference.Should().Be("ETA-ACK-001");
    }

    [Fact]
    public void MarkSubmitted_AfterAcknowledged_Throws()
    {
        var r = NewMonthly();
        r.MarkSubmitted("ETA-VAT-001", User, T0);
        r.MarkAcknowledged("ETA-ACK-001", User, T0.AddHours(2));

        var act = () => r.MarkSubmitted("ETA-VAT-002", User, T0.AddDays(1));
        act.Should().Throw<InvalidOperationException>().WithMessage("*already acknowledged*");
    }

    private static VatReturn NewMonthly() => new(
        periodKind: VatReturnPeriodKind.VatMonthly,
        periodYear: 2026,
        periodOrdinal: 5,
        periodStart: new DateOnly(2026, 5, 1),
        periodEnd: new DateOnly(2026, 5, 31),
        outputVat: MoneyEgp.From(15000m),
        inputVatRecoverable: MoneyEgp.From(4500m),
        inputVatNonRecoverable: MoneyEgp.From(700m),
        netPayable: MoneyEgp.From(10500m),
        contributingDocumentCount: 47,
        generatedAtUtc: T0,
        generatedByUserId: User);
}
