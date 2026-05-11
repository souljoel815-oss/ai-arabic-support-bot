using EgyptTax.Domain.Referrals;

namespace EgyptTax.UnitTests.Domain.Referrals;

/// <summary>
/// G4.3 — covers <see cref="CustomerReferral"/> construction guards
/// + Invited → Installed → Purchased lifecycle, including the
/// backfill behaviour when a referral jumps directly to Purchased.
/// </summary>
public class CustomerReferralTests
{
    private static readonly DateTime T0 = new(2026, 5, 11, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_DefaultsToInvited()
    {
        var r = New();
        r.Status.Should().Be(CustomerReferralStatus.Invited);
        r.RewardDays.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankName(string? bad)
    {
        var act = () => new CustomerReferral(bad!, T0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_TrimsAndPreservesFields()
    {
        var r = new CustomerReferral(
            referredContactName: "  Hany  ",
            invitedAtUtc: T0,
            referredContactDetail: "  +201001234567 ",
            note: "  testing ");

        r.ReferredContactName.Should().Be("Hany");
        r.ReferredContactDetail.Should().Be("+201001234567");
        r.Note.Should().Be("testing");
        r.InvitedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void Constructor_BlankDetail_NormalisesToNull()
    {
        var r = new CustomerReferral("Hany", T0, referredContactDetail: "   ");
        r.ReferredContactDetail.Should().BeNull();
    }

    [Fact]
    public void MarkInstalled_FlipsStatus_AndStoresTimestamp()
    {
        var r = New();
        r.MarkInstalled(T0.AddDays(2));

        r.Status.Should().Be(CustomerReferralStatus.Installed);
        r.InstalledAtUtc.Should().Be(T0.AddDays(2));
    }

    [Fact]
    public void MarkInstalled_Idempotent_PreservesOriginalTimestamp()
    {
        var r = New();
        r.MarkInstalled(T0.AddDays(2));
        r.MarkInstalled(T0.AddDays(5));
        r.InstalledAtUtc.Should().Be(T0.AddDays(2));
    }

    [Fact]
    public void MarkInstalled_NoOpAfterPurchased()
    {
        var r = New();
        r.MarkPurchased(T0.AddDays(5), referredCustomerHwid: "HWID-1");
        r.MarkInstalled(T0.AddDays(10));

        r.Status.Should().Be(CustomerReferralStatus.Purchased);
    }

    [Fact]
    public void MarkPurchased_FlipsStatus_AndAwardsReward()
    {
        var r = New();
        r.MarkPurchased(T0.AddDays(5), referredCustomerHwid: "hwid-001");

        r.Status.Should().Be(CustomerReferralStatus.Purchased);
        r.PurchasedAtUtc.Should().Be(T0.AddDays(5));
        r.RewardDays.Should().Be(30);
        r.ReferredCustomerHwid.Should().Be("HWID-001");
    }

    [Fact]
    public void MarkPurchased_BackfillsInstalledTimestamp()
    {
        // Operator skipped right to "Purchased" — we backfill the
        // Installed timestamp so the timeline isn't full of nulls.
        var r = New();
        r.MarkPurchased(T0.AddDays(5), referredCustomerHwid: null);

        r.InstalledAtUtc.Should().Be(T0.AddDays(5));
    }

    [Fact]
    public void MarkPurchased_PreservesPriorInstalledTimestamp()
    {
        var r = New();
        r.MarkInstalled(T0.AddDays(2));
        r.MarkPurchased(T0.AddDays(5), referredCustomerHwid: null);

        r.InstalledAtUtc.Should().Be(T0.AddDays(2)); // preserved
    }

    [Fact]
    public void MarkPurchased_Idempotent_PreservesOriginalReward()
    {
        var r = New();
        r.MarkPurchased(T0.AddDays(5), null, rewardDays: 30);
        r.MarkPurchased(T0.AddDays(10), null, rewardDays: 60); // would change

        r.PurchasedAtUtc.Should().Be(T0.AddDays(5));
        r.RewardDays.Should().Be(30); // first wins
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void MarkPurchased_RejectsOutOfRangeReward(int bad)
    {
        var r = New();
        var act = () => r.MarkPurchased(T0.AddDays(5), null, rewardDays: bad);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateNote_TrimsAndNullsBlank()
    {
        var r = New();
        r.UpdateNote("   ");
        r.Note.Should().BeNull();

        r.UpdateNote("  hello ");
        r.Note.Should().Be("hello");
    }

    [Fact]
    public void UpdateContactDetail_TrimsAndNullsBlank()
    {
        var r = New();
        r.UpdateContactDetail("   ");
        r.ReferredContactDetail.Should().BeNull();

        r.UpdateContactDetail("  +201001234567 ");
        r.ReferredContactDetail.Should().Be("+201001234567");
    }

    private static CustomerReferral New() =>
        new(referredContactName: "Hany Khaled", invitedAtUtc: T0);
}
