using EgyptTax.Portal.Application.Email;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Portal.Infrastructure.Email;

/// <summary>T031. Development email impl — prints everything to stdout via ILogger.</summary>
internal sealed class DevStdoutEmailService : IEmailService
{
    private readonly ILogger<DevStdoutEmailService> _logger;

    public DevStdoutEmailService(ILogger<DevStdoutEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "=== DEV EMAIL ===\nTo: {ToName} <{To}>\nLocale: {Locale}\nSubject: {Subject}\n---\n{Text}\n=== END EMAIL ===",
            message.ToName,
            message.ToAddress,
            message.Locale,
            message.Subject,
            message.TextBody);

        return Task.CompletedTask;
    }
}
