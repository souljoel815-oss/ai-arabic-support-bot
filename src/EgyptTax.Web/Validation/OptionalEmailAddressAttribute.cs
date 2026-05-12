using System.ComponentModel.DataAnnotations;

namespace EgyptTax.Web.Validation;

/// <summary>
/// Like <see cref="EmailAddressAttribute"/> but treats empty/null as
/// valid — for "(optional)" email fields where the user can leave the
/// field blank but a non-blank value must still be a real email.
///
/// Stock <c>[EmailAddress]</c> rejects "" because the underlying
/// regex doesn't match an empty string. This causes the surprising
/// "The Email field is not a valid e-mail address" error on
/// optional-but-blank fields. See BUG-003 in the May 2026 testing
/// report.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class OptionalEmailAddressAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute Inner = new();

    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is string s && string.IsNullOrWhiteSpace(s)) return true;
        return Inner.IsValid(value);
    }

    public override string FormatErrorMessage(string name) =>
        $"The {name} field must be a valid email address (or left blank).";
}
