using EgyptTax.Application.Common.Abstractions;
using EgyptTax.Domain.Audit;
using FluentValidation;
using MediatR;

namespace EgyptTax.UnitTests.Application.Behaviors;

// Fakes used across the 5 pipeline-behaviour tests. Kept in one file so the
// individual test files stay small and focused.

public sealed record PingQuery(string Message) : IRequest<string>;

public sealed record PingCommand(string Message) : ICommand<string>
{
    public string Message { get; init; } = Message;
}

public sealed record AuditedPingCommand(string Message) : ICommand<string>, IAuditableRequest
{
    public AuditLogPayload BuildAuditPayload(object? result, ICurrentUser currentUser) =>
        new(
            Kind: "AuditedPing",
            ActorUserId: currentUser.UserId,
            ActorFirmName: currentUser.FirmName,
            CompanyId: currentUser.CompanyId ?? Guid.Empty,
            PayloadJson: $$"""{"message":"{{Message}}","result":"{{result}}"}""");
}

public sealed record AuthorizedPingCommand(string Message) : ICommand<string>, IAuthorizedRequest;

public sealed class PingCommandValidator : AbstractValidator<PingCommand>
{
    public PingCommandValidator()
    {
        RuleFor(c => c.Message).NotEmpty().MinimumLength(2);
    }
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; init; }
    public string? FirmName { get; init; }
    public Guid? CompanyId { get; init; }
    public bool IsAuthenticated => UserId is not null;

    public static FakeCurrentUser Anonymous { get; } = new();

    public static FakeCurrentUser Authenticated(Guid? id = null) => new()
    {
        UserId = id ?? Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
    };
}
