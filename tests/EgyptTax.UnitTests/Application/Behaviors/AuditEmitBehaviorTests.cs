using EgyptTax.Application.Audit;
using EgyptTax.Application.Common.Behaviors;
using EgyptTax.Domain.Audit;
using MediatR;
using NSubstitute;

namespace EgyptTax.UnitTests.Application.Behaviors;

public class AuditEmitBehaviorTests
{
    [Fact]
    public async Task AuditableRequest_HandlerSucceeds_EmitsAuditRow()
    {
        var store = Substitute.For<IAuditLogStore>();
        var user = FakeCurrentUser.Authenticated();
        var sut = new AuditEmitBehavior<AuditedPingCommand, string>(store, user);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new AuditedPingCommand("hi"), next, CancellationToken.None);

        result.Should().Be("ok");
        await store.Received(1).AppendAsync(
            Arg.Is<AuditLogPayload>(p =>
                p.Kind == "AuditedPing" &&
                p.ActorUserId == user.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NonAuditableRequest_DoesNotEmit()
    {
        var store = Substitute.For<IAuditLogStore>();
        var sut = new AuditEmitBehavior<PingCommand, string>(store, FakeCurrentUser.Authenticated());
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        await sut.Handle(new PingCommand("hi"), next, CancellationToken.None);

        await store.DidNotReceiveWithAnyArgs().AppendAsync(default!, default);
    }

    [Fact]
    public async Task AuditableRequest_HandlerThrows_DoesNotEmit()
    {
        var store = Substitute.For<IAuditLogStore>();
        var sut = new AuditEmitBehavior<AuditedPingCommand, string>(store, FakeCurrentUser.Authenticated());
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns<Task<string>>(_ => throw new InvalidOperationException("boom"));

        var act = async () => await sut.Handle(new AuditedPingCommand("hi"), next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await store.DidNotReceiveWithAnyArgs().AppendAsync(default!, default);
    }
}
