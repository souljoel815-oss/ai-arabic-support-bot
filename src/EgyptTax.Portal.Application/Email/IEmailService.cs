namespace EgyptTax.Portal.Application.Email;

/// <summary>
/// T031. Port for transactional email. Production impl is Resend (per
/// research §4); Development impl prints to stdout so the docker-compose
/// fake-SMTP scenario works without API keys.
/// </summary>
public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    string Locale = "ar-EG",
    string? FromName = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public sealed record EmailAttachment(string FileName, string MediaType, byte[] Content);
