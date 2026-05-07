using FluentValidation.Results;

namespace EgyptTax.Application.Common.Exceptions;

/// <summary>
/// Raised by <c>ValidationBehavior</c> when one or more <c>IValidator</c>
/// instances flag a request as invalid. Aggregates all
/// <see cref="ValidationFailure"/>s so the caller can surface every error.
/// </summary>
public sealed class ValidationException : Exception
{
    public IReadOnlyList<ValidationFailure> Errors { get; }

    public ValidationException(IEnumerable<ValidationFailure> errors)
        : base("One or more validation failures occurred.")
    {
        Errors = errors.ToList();
    }
}
