using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.FirmPortal;
using EgyptTax.Web.Logging;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EgyptTax.UnitTests.Web.Logging;

/// <summary>
/// T256 / R-20 — pins the CorrelationContextMiddleware contract:
/// (1) generates a fresh Guid CorrelationId when the header is
/// missing / empty / non-Guid (defends against hostile header
/// poisoning); (2) preserves a valid incoming Guid header so the
/// client-server correlation works when a frontend passes its own
/// id; (3) ALWAYS echoes the resolved id back on the response so a
/// support engineer can grab it from a screenshot; (4) pushes
/// CorrelationId / UserId / FirmName onto Serilog's LogContext so
/// every downstream log line in the request carries them.
/// </summary>
public class CorrelationContextMiddlewareTests : IDisposable
{
    private readonly CapturingSink _sink = new();
    private readonly Logger _logger;

    public CorrelationContextMiddlewareTests()
    {
        _logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(_sink)
            .CreateLogger();
        Log.Logger = _logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task NoHeader_GeneratesFreshGuid_AndEchoesItBack()
    {
        var ctx = new DefaultHttpContext();
        var sut = BuildMiddleware();

        await sut.InvokeAsync(ctx, AnonymousUser(), EmptyResolver());

        var echoed = ctx.Response.Headers[CorrelationContextMiddleware.CorrelationIdHeader].ToString();
        echoed.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(echoed, out var parsed).Should().BeTrue(
            because: "the echoed header MUST be a parseable Guid so clients can store + replay it");
        parsed.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ValidIncomingGuid_IsPreserved()
    {
        var incoming = Guid.NewGuid();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[CorrelationContextMiddleware.CorrelationIdHeader] = incoming.ToString("D");

        var sut = BuildMiddleware();
        await sut.InvokeAsync(ctx, AnonymousUser(), EmptyResolver());

        ctx.Response.Headers[CorrelationContextMiddleware.CorrelationIdHeader].ToString()
            .Should().Be(incoming.ToString("D"),
                because: "a valid Guid header from the client MUST be preserved so frontend + backend logs share one id");
    }

    [Fact]
    public async Task NonGuidHeader_IsRejected_FreshGuidGenerated()
    {
        // Defends against header poisoning — a hostile client sending
        // CorrelationId: "<script>" would otherwise propagate that
        // string into structured log fields + downstream queries.
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[CorrelationContextMiddleware.CorrelationIdHeader] = "<not-a-guid>";

        var sut = BuildMiddleware();
        await sut.InvokeAsync(ctx, AnonymousUser(), EmptyResolver());

        var echoed = ctx.Response.Headers[CorrelationContextMiddleware.CorrelationIdHeader].ToString();
        Guid.TryParse(echoed, out _).Should().BeTrue(
            because: "non-Guid header MUST be replaced with a fresh server-generated Guid");
    }

    [Fact]
    public async Task LogContext_CarriesCorrelationId_UserId_FirmName_OnEveryLine()
    {
        var actorId = Guid.NewGuid();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[CorrelationContextMiddleware.CorrelationIdHeader] = "11111111-1111-1111-1111-111111111111";

        var resolver = Substitute.For<IFirmContextResolver>();
        resolver.ResolveFirmNameAsync(actorId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("Nile Accounting LLC"));

        var sut = new CorrelationContextMiddleware(_ =>
        {
            // Inside the middleware-wrapped delegate, a log call MUST
            // see the enriched LogContext.
            Log.Information("inside-handler");
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(ctx, AuthenticatedUser(actorId), resolver);

        _sink.Events.Should().ContainSingle(e => e.MessageTemplate.Text == "inside-handler");
        var captured = _sink.Events.Single(e => e.MessageTemplate.Text == "inside-handler");
        captured.Properties.Should().ContainKey("CorrelationId");
        captured.Properties["CorrelationId"].ToString().Trim('"')
            .Should().Be("11111111-1111-1111-1111-111111111111");
        captured.Properties.Should().ContainKey("UserId");
        captured.Properties["UserId"].ToString().Trim('"')
            .Should().Be(actorId.ToString("D"));
        captured.Properties.Should().ContainKey("FirmName");
        captured.Properties["FirmName"].ToString().Trim('"')
            .Should().Be("Nile Accounting LLC");
    }

    [Fact]
    public async Task LogContext_DoesNotPersist_BeyondTheRequest()
    {
        // After the middleware returns, a subsequent log call MUST
        // NOT see this request's CorrelationId — otherwise we'd
        // cross-contaminate concurrent requests on shared loggers.
        var ctx = new DefaultHttpContext();
        var sut = BuildMiddleware();
        await sut.InvokeAsync(ctx, AnonymousUser(), EmptyResolver());

        Log.Information("after-handler");

        var afterEvent = _sink.Events.Single(e => e.MessageTemplate.Text == "after-handler");
        afterEvent.Properties.Should().NotContainKey("CorrelationId",
            because: "LogContext.PushProperty disposes when the using-block exits — context is request-scoped");
    }

    private static CorrelationContextMiddleware BuildMiddleware() =>
        new(_ => Task.CompletedTask);

    private static ICurrentUser AnonymousUser()
    {
        var u = Substitute.For<ICurrentUser>();
        u.UserId.Returns((Guid?)null);
        return u;
    }

    private static ICurrentUser AuthenticatedUser(Guid id)
    {
        var u = Substitute.For<ICurrentUser>();
        u.UserId.Returns(id);
        return u;
    }

    private static IFirmContextResolver EmptyResolver()
    {
        var r = Substitute.For<IFirmContextResolver>();
        r.ResolveFirmNameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));
        return r;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
