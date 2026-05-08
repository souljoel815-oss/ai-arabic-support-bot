using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.FirmPortal;
using Serilog.Context;

namespace EgyptTax.Web.Logging;

/// <summary>
/// T256 / R-20 — pushes per-request correlation context onto Serilog's
/// <see cref="LogContext"/> so EVERY operational log line written
/// during the request carries:
/// <list type="bullet">
///   <item><c>CorrelationId</c> — incoming <c>X-Correlation-Id</c>
///   header if present + non-empty (validated as a Guid so a hostile
///   client can't poison the field with arbitrary text), otherwise a
///   freshly-allocated Guid. Always echoed back on the response so a
///   client can correlate a screenshot of an error to a specific
///   server log line.</item>
///   <item><c>UserId</c> — claim-derived; absent on anonymous routes
///   (login, password reset).</item>
///   <item><c>FirmName</c> — looked up via
///   <see cref="IFirmContextResolver"/> when the actor is a known
///   firm user; absent for in-house actors. Lets the inspector
///   filter ops logs by firm without a join.</item>
/// </list>
///
/// Middleware order matters: this MUST come AFTER UseAuthentication
/// (so the claims principal is populated) but BEFORE the Blazor /
/// MVC routing handlers (so every downstream log line in the request
/// path sees the context).
///
/// Distinct from the FR-028 audit log — that one is the cryptographic
/// chain of business events; THIS is the operational log used by ops
/// for debugging + incident triage.
/// </summary>
public sealed class CorrelationContextMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        ICurrentUser currentUser,
        IFirmContextResolver firmContextResolver
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var correlationId = ResolveCorrelationId(httpContext);
        httpContext.Response.Headers[CorrelationIdHeader] = correlationId.ToString("D");

        using (LogContext.PushProperty("CorrelationId", correlationId.ToString("D")))
        {
            var userId = currentUser.UserId;
            string? firmName = null;
            if (userId is { } id && id != Guid.Empty)
            {
                firmName = await firmContextResolver.ResolveFirmNameAsync(
                    id,
                    httpContext.RequestAborted
                );
            }

            using (
                LogContext.PushProperty("UserId", userId?.ToString("D"), destructureObjects: false)
            )
            using (LogContext.PushProperty("FirmName", firmName, destructureObjects: false))
            {
                await _next(httpContext);
            }
        }
    }

    private static Guid ResolveCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValues))
        {
            var raw = headerValues.ToString();
            if (
                !string.IsNullOrWhiteSpace(raw)
                && Guid.TryParse(raw, out var fromHeader)
                && fromHeader != Guid.Empty
            )
            {
                return fromHeader;
            }
        }
        return Guid.NewGuid();
    }
}
