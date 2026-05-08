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
        var sut = new AuditEmitBehavior<AuditedPingCommand, string>(store, user, FakeFirmContextResolver.Empty);
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
        var sut = new AuditEmitBehavior<PingCommand, string>(store, FakeCurrentUser.Authenticated(), FakeFirmContextResolver.Empty);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        await sut.Handle(new PingCommand("hi"), next, CancellationToken.None);

        await store.DidNotReceiveWithAnyArgs().AppendAsync(default!, default);
    }

    [Fact]
    public async Task AuditableRequest_FirmUserActor_OverlaysFirmName()
    {
        // T225 / FR-049 / US8 — when the request builder didn't
        // populate ActorFirmName (the typical case for normal
        // documents authored by an actor who happens to be a firm
        // user) the behavior MUST overlay it from the resolver so
        // the audit chain still tags the firm.
        var store = Substitute.For<IAuditLogStore>();
        var actorId = Guid.NewGuid();
        var user = new FakeCurrentUser { UserId = actorId, FirmName = null, CompanyId = Guid.NewGuid() };
        var resolver = new FakeFirmContextResolver().Map(actorId, "Nile Accounting LLC");
        var sut = new AuditEmitBehavior<AuditedPingCommand, string>(store, user, resolver);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        await sut.Handle(new AuditedPingCommand("hi"), next, CancellationToken.None);

        await store.Received(1).AppendAsync(
            Arg.Is<AuditLogPayload>(p =>
                p.ActorUserId == actorId &&
                p.ActorFirmName == "Nile Accounting LLC"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditableRequest_InHouseActor_LeavesFirmNameNull()
    {
        // In-house users (no AccountantFirmUser row) come back null
        // from the resolver — payload.ActorFirmName stays null,
        // not "" or some placeholder.
        var store = Substitute.For<IAuditLogStore>();
        var actorId = Guid.NewGuid();
        var user = new FakeCurrentUser { UserId = actorId, FirmName = null, CompanyId = Guid.NewGuid() };
        var sut = new AuditEmitBehavior<AuditedPingCommand, string>(store, user, FakeFirmContextResolver.Empty);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        await sut.Handle(new AuditedPingCommand("hi"), next, CancellationToken.None);

        await store.Received(1).AppendAsync(
            Arg.Is<AuditLogPayload>(p =>
                p.ActorUserId == actorId &&
                p.ActorFirmName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditableRequest_HandlerThrows_DoesNotEmit()
    {
        var store = Substitute.For<IAuditLogStore>();
        var sut = new AuditEmitBehavior<AuditedPingCommand, string>(store, FakeCurrentUser.Authenticated(), FakeFirmContextResolver.Empty);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns<Task<string>>(_ => throw new InvalidOperationException("boom"));

        var act = async () => await sut.Handle(new AuditedPingCommand("hi"), next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await store.DidNotReceiveWithAnyArgs().AppendAsync(default!, default);
    }
}
