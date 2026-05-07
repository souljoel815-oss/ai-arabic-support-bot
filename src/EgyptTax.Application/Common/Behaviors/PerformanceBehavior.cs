using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Application.Common.Behaviors;

/// <summary>
/// Times each handler. Logs a Warning if the handler exceeded the
/// <see cref="SlowThreshold"/> (500 ms by default per research.md R-20).
/// Always re-throws on exceptions so the pipeline's error semantics are
/// unaffected; the timer is stopped before re-throwing so the warning
/// log includes the full failed duration.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await next();
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.Elapsed > SlowThreshold)
            {
                _logger.LogWarning(
                    "Slow handler: {RequestType} took {ElapsedMs} ms (threshold {ThresholdMs} ms)",
                    typeof(TRequest).Name,
                    stopwatch.ElapsedMilliseconds,
                    (int)SlowThreshold.TotalMilliseconds);
            }
        }
    }
}
