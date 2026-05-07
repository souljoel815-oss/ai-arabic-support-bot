using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace EgyptTax.SharedKernel;

/// <summary>
/// Egyptian Tax Identification Number — exactly 9 numeric digits per the
/// publicly documented ETA format. Format-only validation in the MVP per
/// research.md R-13; the supplier-TIN periodic re-validation cron (FR-043
/// + Differentiator 1) verifies live registry membership.
/// </summary>
public readonly partial record struct EgyptianTin
{
    private static readonly Regex Format = NineDigitRegex();

    public string Value { get; }

    private EgyptianTin(string value)
    {
        Value = value;
    }

    public static bool TryParse(string? raw, [NotNullWhen(true)] out EgyptianTin? tin)
    {
        if (raw is not null && Format.IsMatch(raw))
        {
            tin = new EgyptianTin(raw);
            return true;
        }

        tin = null;
        return false;
    }

    public static EgyptianTin Parse(string raw) =>
        TryParse(raw, out var tin)
            ? tin.Value
            : throw new FormatException(
                $"'{raw}' is not a valid Egyptian TIN (must be exactly 9 numeric digits).");

    public override string ToString() => Value;

    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex NineDigitRegex();
}
