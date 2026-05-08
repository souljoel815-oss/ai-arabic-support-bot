using System.Globalization;

namespace EgyptTax.SharedKernel;

/// <summary>
/// Egyptian Pound monetary value. EGP-only by spec assumption (single-currency
/// MVP). Underlying storage is decimal at full precision; the
/// <see cref="AmountRoundedToCents"/> projection applies banker's rounding
/// (MidpointRounding.ToEven) per the spec edge case "Rounding".
/// </summary>
public readonly record struct MoneyEgp(decimal Amount) : IComparable<MoneyEgp>
{
    public static MoneyEgp Zero { get; } = new(0m);

    public static MoneyEgp From(decimal amount) => new(amount);

    /// <summary>
    /// Amount rounded to 2 decimal places using banker's rounding (ToEven),
    /// per the spec edge case "Rounding".
    /// </summary>
    public decimal AmountRoundedToCents => Math.Round(Amount, 2, MidpointRounding.ToEven);

    public static MoneyEgp operator +(MoneyEgp left, MoneyEgp right) =>
        new(left.Amount + right.Amount);

    public static MoneyEgp operator -(MoneyEgp left, MoneyEgp right) =>
        new(left.Amount - right.Amount);

    public static MoneyEgp operator *(MoneyEgp left, decimal scalar) => new(left.Amount * scalar);

    public static MoneyEgp operator *(decimal scalar, MoneyEgp right) => new(scalar * right.Amount);

    public static bool operator <(MoneyEgp left, MoneyEgp right) => left.Amount < right.Amount;

    public static bool operator >(MoneyEgp left, MoneyEgp right) => left.Amount > right.Amount;

    public static bool operator <=(MoneyEgp left, MoneyEgp right) => left.Amount <= right.Amount;

    public static bool operator >=(MoneyEgp left, MoneyEgp right) => left.Amount >= right.Amount;

    public int CompareTo(MoneyEgp other) => Amount.CompareTo(other.Amount);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"EGP {AmountRoundedToCents:F2}");
}
