using EgyptTax.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using NSubstitute;
using ValidationException = EgyptTax.Application.Common.Exceptions.ValidationException;

namespace EgyptTax.UnitTests.Application.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task NoValidators_PassesThroughToHandler()
    {
        var sut = new ValidationBehavior<PingCommand, string>(Array.Empty<IValidator<PingCommand>>());
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new PingCommand("hi"), next, CancellationToken.None);

        result.Should().Be("ok");
        await next.Received(1)();
    }

    [Fact]
    public async Task ValidatorsPass_CallsHandler()
    {
        var sut = new ValidationBehavior<PingCommand, string>(new[] { new PingCommandValidator() });
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns(Task.FromResult("ok"));

        var result = await sut.Handle(new PingCommand("hello"), next, CancellationToken.None);

        result.Should().Be("ok");
        await next.Received(1)();
    }

    [Fact]
    public async Task ValidatorFails_ThrowsValidationException_AndDoesNotCallHandler()
    {
        var sut = new ValidationBehavior<PingCommand, string>(new[] { new PingCommandValidator() });
        var next = Substitute.For<RequestHandlerDelegate<string>>();

        var act = async () => await sut.Handle(new PingCommand(""), next, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().NotBeEmpty();
        await next.DidNotReceiveWithAnyArgs()();
    }
}
