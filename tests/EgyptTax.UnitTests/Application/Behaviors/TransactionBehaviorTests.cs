using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Application.Common.Behaviors;
using MediatR;
using NSubstitute;

namespace EgyptTax.UnitTests.Application.Behaviors;

public class TransactionBehaviorTests
{
    [Fact]
    public async Task Command_HandlerSucceeds_BeginsAndCommits()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var sut = new TransactionBehavior<PingCommand, string>(uow);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new PingCommand("x"), next, CancellationToken.None);

        result.Should().Be("ok");
        Received.InOrder(() =>
        {
            uow.BeginTransactionAsync(Arg.Any<CancellationToken>());
            next();
            uow.CommitAsync(Arg.Any<CancellationToken>());
        });
        await uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command_HandlerThrows_RollsBackAndRethrows()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var sut = new TransactionBehavior<PingCommand, string>(uow);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns<Task<string>>(_ => throw new InvalidOperationException("boom"));

        var act = async () => await sut.Handle(new PingCommand("x"), next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        await uow.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Query_DoesNotOpenTransaction()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var sut = new TransactionBehavior<PingQuery, string>(uow);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new PingQuery("x"), next, CancellationToken.None);

        result.Should().Be("ok");
        await uow.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }
}
