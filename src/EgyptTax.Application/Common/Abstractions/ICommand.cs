using MediatR;

namespace EgyptTax.Application.Common.Abstractions;

/// <summary>
/// Marker for state-changing operations. <c>TransactionBehavior</c> wraps
/// commands in a DB transaction; queries (<see cref="IRequest{TResponse}"/>
/// without this marker) skip the transaction overhead.
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
