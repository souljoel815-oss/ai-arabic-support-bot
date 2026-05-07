using EgyptTax.Application.Common.Behaviors;
using EgyptTax.Application.Common.Exceptions;
using MediatR;
using NSubstitute;

namespace EgyptTax.UnitTests.Application.Behaviors;

public class AuthorizationBehaviorTests
{
    [Fact]
    public async Task AuthorizedRequest_AuthenticatedUser_CallsHandler()
    {
        var sut = new AuthorizationBehavior<AuthorizedPingCommand, string>(FakeCurrentUser.Authenticated());
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new AuthorizedPingCommand("hi"), next, CancellationToken.None);

        result.Should().Be("ok");
        await next.Received(1)();
    }

    [Fact]
    public async Task AuthorizedRequest_AnonymousUser_ThrowsUnauthorized()
    {
        var sut = new AuthorizationBehavior<AuthorizedPingCommand, string>(FakeCurrentUser.Anonymous);
        var next = Substitute.For<RequestHandlerDelegate<string>>();

        var act = async () => await sut.Handle(new AuthorizedPingCommand("hi"), next, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        await next.DidNotReceiveWithAnyArgs()();
    }

    [Fact]
    public async Task NonAuthorizedRequest_AnonymousUser_StillCallsHandler()
    {
        var sut = new AuthorizationBehavior<PingCommand, string>(FakeCurrentUser.Anonymous);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new PingCommand("hi"), next, CancellationToken.None);

        result.Should().Be("ok");
        await next.Received(1)();
    }
}
