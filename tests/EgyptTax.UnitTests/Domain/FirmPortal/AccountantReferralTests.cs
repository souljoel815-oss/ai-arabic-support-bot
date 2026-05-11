using EgyptTax.Domain.FirmPortal;

namespace EgyptTax.UnitTests.Domain.FirmPortal;

/// <summary>
/// G1.4 — covers <see cref="AccountantReferral"/> commission math
/// + Pending → Earned → Paid lifecycle + invariants.
/// </summary>
public class AccountantReferralTests
{
    private static readonly Guid Firm = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ComputesCommissionAtTwentyPercentOfPrice()
    {
        var r = New(price: 5000m, rate: 20m);

        r.LicenseAnnualPriceEgp.Should().Be(5000m);
        r.CommissionRatePercent.Should().Be(20m);
        r.CommissionEgp.Should().Be(1000m);
        r.Status.Should().Be(AccountantReferralStatus.Pending);
    }

    [Fact]
    public void Constructor_RoundsCommissionToTwoDecimals()
    {
        // 3,333 EGP × 20% = 666.60 (no fractional fils)
        var r = New(price: 3333m, rate: 20m);
        r.CommissionEgp.Should().Be(666.60m);
    }

    [Fact]
    public void Constructor_NormalisesHwidToUppercase()
    {
        var r = New(hwid: "abcd-1234-ef56-7890");
        r.ReferredCustomerHwid.Should().Be("ABCD-1234-EF56-7890");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Constructor_RejectsNonPositivePrice(decimal badPrice)
    {
        var act = () => New(price: badPrice);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_RejectsCommissionOutsideZeroToHundred(decimal badRate)
    {
        var act = () => New(rate: badRate);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkEarned_FlipsStatus_AndStoresTimestamp()
    {
        var r = New();
        r.MarkEarned(T0);

        r.Status.Should().Be(AccountantReferralStatus.Earned);
        r.EarnedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void MarkEarned_IsIdempotent_PreservesOriginalTimestamp()
    {
        var r = New();
        r.MarkEarned(T0);
        r.MarkEarned(T0.AddDays(5));

        r.EarnedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void MarkEarned_AfterPaid_Throws()
    {
        var r = New();
        r.MarkEarned(T0);
        r.MarkPaid(T0.AddDays(1), "INSTAPAY-001");

        var act = () => r.MarkEarned(T0.AddDays(2));
        act.Should().Throw<InvalidOperationException>().WithMessage("*already Paid*");
    }

    [Fact]
    public void MarkPaid_Pending_Throws()
    {
        var r = New();
        var act = () => r.MarkPaid(T0, "INSTAPAY-001");
        act.Should().Throw<InvalidOperationException>().WithMessage("*still Pending*");
    }

    [Fact]
    public void MarkPaid_Earned_Succeeds_AndStoresReference()
    {
        var r = New();
        r.MarkEarned(T0);
        r.MarkPaid(T0.AddDays(1), "INSTAPAY-XYZ");

        r.Status.Should().Be(AccountantReferralStatus.Paid);
        r.PaidAtUtc.Should().Be(T0.AddDays(1));
        r.PaidReference.Should().Be("INSTAPAY-XYZ");
    }

    [Fact]
    public void MarkPaid_AlreadyPaid_Throws()
    {
        var r = New();
        r.MarkEarned(T0);
        r.MarkPaid(T0.AddDays(1), "FIRST");

        var act = () => r.MarkPaid(T0.AddDays(2), "SECOND");
        act.Should().Throw<InvalidOperationException>().WithMessage("*already Paid*");
    }

    [Fact]
    public void MarkPaid_BlankReference_Throws()
    {
        var r = New();
        r.MarkEarned(T0);
        var act = () => r.MarkPaid(T0.AddDays(1), "   ");
        act.Should().Throw<ArgumentException>();
    }

    private static AccountantReferral New(
        decimal price = 3000m,
        decimal rate = 20m,
        string hwid = "ABCD-1234-EF56-7890",
        string customer = "Hope Co")
        => new(
            accountantFirmUserId: Firm,
            referredCustomerName: customer,
            referredCustomerHwid: hwid,
            licenseEdition: "Standard",
            licenseAnnualPriceEgp: price,
            commissionRatePercent: rate,
            referredAtUtc: T0);
}
