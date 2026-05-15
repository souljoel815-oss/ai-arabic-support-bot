using System.Net;
using System.Net.Mail;
using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Settings;

namespace EgyptTax.Infrastructure.Crm;

/// <summary>
/// v5 B.3 (phase 1, send-only) — send a free-form email to a Lead
/// from the lead detail page. Mirrors
/// <see cref="SendInvoiceByEmailHandler"/>'s SMTP wiring; the
/// caller is responsible for persisting the LeadActivity row that
/// records the send.
///
/// Phase 2 (IMAP receive + thread matching) deferred until a real
/// customer asks per the v5 §3 spec.
/// </summary>
public sealed class SendLeadEmailHandler
{
    private readonly SmtpPasswordProtector _protector;

    public SendLeadEmailHandler(SmtpPasswordProtector protector) => _protector = protector;

    public async Task<SmtpTestResult> SendAsync(
        SmtpSettings settings,
        string toAddress,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        if (settings.SendMethod != EmailSendMethod.DirectSmtp)
        {
            return SmtpTestResult.Failure(
                "Email send requires Direct-SMTP mode. Configure it under Settings → Email Settings.");
        }
        if (string.IsNullOrWhiteSpace(settings.Server)
            || string.IsNullOrWhiteSpace(settings.Username)
            || string.IsNullOrWhiteSpace(settings.EncryptedPassword)
            || string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            return SmtpTestResult.Failure("SMTP settings incomplete (server/username/password/from required).");
        }
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return SmtpTestResult.Failure("Recipient address is empty — set the lead's email first.");
        }
        if (string.IsNullOrWhiteSpace(subject))
        {
            return SmtpTestResult.Failure("Subject is required.");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            return SmtpTestResult.Failure("Body is required.");
        }

        try
        {
            var password = _protector.Decrypt(settings.EncryptedPassword);
            using var client = new SmtpClient(settings.Server, settings.Port)
            {
                EnableSsl = settings.UseTls,
                Credentials = new NetworkCredential(settings.Username, password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromAddress, settings.FromName ?? settings.FromAddress),
                Subject = subject.Trim(),
                Body = body,
                IsBodyHtml = false,
            };
            message.To.Add(toAddress.Trim());
            await client.SendMailAsync(message, ct);
            return SmtpTestResult.Success($"Sent to {toAddress.Trim()}.");
        }
        catch (SmtpException ex)
        {
            return SmtpTestResult.Failure($"SMTP error ({ex.StatusCode}): {ex.Message}");
        }
        catch (Exception ex)
        {
            return SmtpTestResult.Failure(ex.Message);
        }
    }
}
