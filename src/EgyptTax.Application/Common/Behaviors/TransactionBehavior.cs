using EgyptTax.Application.Common.Abstractions;
using MediatR;

namespace EgyptTax.Application.Common.Behaviors;

/// <summary>
/// Wraps every <see cref="ICommand{TResponse}"/> in a unit-of-work
/// transaction; commits when the inner pipeline (including AuditEmit)
/// returns successfully, rolls back on any exception. Queries
/// (<see cref="IRequest{TResponse}"/> without the <c>ICommand</c> marker)
/// pass through without opening a transaction so read paths stay light.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not ICommand<TResponse>)
        {
            return await next();
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await _unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
