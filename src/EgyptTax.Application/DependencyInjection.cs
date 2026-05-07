using EgyptTax.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EgyptTax.Application;

/// <summary>
/// Registers MediatR + FluentValidation + the 5 pipeline behaviours in the
/// canonical order. Behaviour order is significant per INV-001:
/// PerformanceBehavior wraps everything (outermost) so it captures the
/// full duration including auth+validation+commit overhead.
/// AuthorizationBehavior comes next so unauthorised requests fail before
/// the more expensive validation+transaction layers run. ValidationBehavior
/// is next so validation failures fail before any transaction is opened.
/// TransactionBehavior wraps AuditEmit + handler so the audit row is part
/// of the same transaction as the change. AuditEmitBehavior is innermost
/// (just outside the handler) so it sees the handler's return value and
/// runs inside the transaction.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuditEmitBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
