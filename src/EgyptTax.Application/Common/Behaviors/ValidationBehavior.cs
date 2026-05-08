using FluentValidation;
using MediatR;

namespace EgyptTax.Application.Common.Behaviors;

using ValidationException = EgyptTax.Application.Common.Exceptions.ValidationException;

/// <summary>
/// Runs every registered <see cref="IValidator{TRequest}"/> for the
/// incoming request before the handler. Aggregates failures into a single
/// <see cref="ValidationException"/>. No-ops when no validators are
/// registered for the request type.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(next);

        var enumerated = _validators as IList<IValidator<TRequest>> ?? _validators.ToList();
        if (enumerated.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            enumerated.Select(v => v.ValidateAsync(context, cancellationToken))
        );
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
