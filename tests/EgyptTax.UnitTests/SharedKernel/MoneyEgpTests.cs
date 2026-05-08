using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.SharedKernel;

public class MoneyEgpTests
{
    [Fact]
    public void Zero_HasZeroAmount()
    {
        MoneyEgp.Zero.Amount.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(123_456_789.12)]
    public void From_AcceptsNonNegativeAndStoresAtDecimalScale4(decimal amount)
    {
        var money = MoneyEgp.From(amount);
        money.Amount.Should().Be(amount);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = MoneyEgp.From(100m);
        var b = MoneyEgp.From(100m);
        var c = MoneyEgp.From(101m);

        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
        (a == c).Should().BeFalse();
    }

    [Fact]
    public void Add_SumsAmounts()
    {
        var sum = MoneyEgp.From(100.50m) + MoneyEgp.From(49.50m);
        sum.Amount.Should().Be(150.00m);
    }

    [Fact]
    public void Subtract_SubtractsAmounts()
    {
        var diff = MoneyEgp.From(100.00m) - MoneyEgp.From(40.00m);
        diff.Amount.Should().Be(60.00m);
    }

    [Fact]
    public void Multiply_ScalesByDecimal()
    {
        var product = MoneyEgp.From(100.00m) * 1.14m;
        // 100 * 1.14 = 114.00
        product.AmountRoundedToCents.Should().Be(114.00m);
    }

    [Theory]
    // Banker's rounding (MidpointRounding.ToEven): 0.5 rounds to nearest even.
    [InlineData(1.005, 1.00)] // halfway to 1.00 (even)
    [InlineData(1.015, 1.02)] // halfway to 1.02 (even)
    [InlineData(1.025, 1.02)] // halfway to 1.02 (even)
    [InlineData(1.035, 1.04)] // halfway to 1.04 (even)
    [InlineData(1.045, 1.04)] // halfway to 1.04 (even)
    [InlineData(1.055, 1.06)] // halfway to 1.06 (even)
    [InlineData(1.234, 1.23)] // ordinary round-down
    [InlineData(1.236, 1.24)] // ordinary round-up
    public void AmountRoundedToCents_AppliesBankersRoundingPerSpecEdgeCase(
        decimal raw,
        decimal expected
    )
    {
        MoneyEgp.From(raw).AmountRoundedToCents.Should().Be(expected);
    }

    [Fact]
    public void Comparison_WorksByAmount()
    {
        var ten = MoneyEgp.From(10m);
        var alsoTen = MoneyEgp.From(10m);
        var twenty = MoneyEgp.From(20m);

        (ten < twenty).Should().BeTrue();
        (twenty > ten).Should().BeTrue();
        (ten <= alsoTen).Should().BeTrue();
        (ten >= alsoTen).Should().BeTrue();
    }

    [Fact]
    public void ToString_RendersWithEgpSymbolAndTwoDecimals()
    {
        MoneyEgp.From(1234.5m).ToString().Should().Be("EGP 1234.50");
    }
}
