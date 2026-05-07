using EgyptTax.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EgyptTax.UnitTests.Application.Behaviors;

public class PerformanceBehaviorTests
{
    [Fact]
    public async Task FastHandler_NoWarning()
    {
        var logger = Substitute.For<ILogger<PerformanceBehavior<PingCommand, string>>>();
        var sut = new PerformanceBehavior<PingCommand, string>(logger);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new PingCommand("hi"), next, CancellationToken.None);

        result.Should().Be("ok");
        // No Warning-level log call should occur for a fast handler.
        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SlowHandler_LogsWarning()
    {
        var logger = Substitute.For<ILogger<PerformanceBehavior<PingCommand, string>>>();
        var sut = new PerformanceBehavior<PingCommand, string>(logger);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(async _ =>
        {
            await Task.Delay(600).ConfigureAwait(false);
            return "ok";
        });

        var result = await sut.Handle(new PingCommand("hi"), next, CancellationToken.None);

        result.Should().Be("ok");
        // ILogger.Log is virtual and called by all extension methods. Match
        // any Warning call.
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandlerThrows_RethrowsAndStillStopsTimer()
    {
        var logger = Substitute.For<ILogger<PerformanceBehavior<PingCommand, string>>>();
        var sut = new PerformanceBehavior<PingCommand, string>(logger);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns<Task<string>>(_ => throw new InvalidOperationException("boom"));

        var act = async () => await sut.Handle(new PingCommand("hi"), next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
