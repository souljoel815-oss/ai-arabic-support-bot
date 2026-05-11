using System.Net;
using System.Net.Mail;
using EgyptTax.Domain.Settings;

namespace EgyptTax.Infrastructure.Settings;

/// <summary>
/// Gux.13 Tab 5 — sends a test email using the configured SMTP
/// settings + the protected password. Returns a friendly result
/// instead of throwing so the UI can render Arabic-localised
/// success / failure without a try/catch in the page.
///
/// Uses System.Net.Mail.SmtpClient directly — adding MailKit /
/// FluentEmail just for one one-shot send is overkill, and the
/// modernised API (MailKit) costs another dependency for no
/// material benefit on a single SMB-grade send.
/// </summary>
public sealed class SmtpTestSender
{
    private readonly SmtpPasswordProtector _protector;

    public SmtpTestSender(SmtpPasswordProtector protector) => _protector = protector;

    public async Task<SmtpTestResult> SendAsync(SmtpSettings settings, string toAddress, CancellationToken ct = default)
    {
        if (settings.SendMethod != EmailSendMethod.DirectSmtp)
            return SmtpTestResult.Failure("Settings are configured for the OS mail client, not direct SMTP. Switch first.");
        if (string.IsNullOrWhiteSpace(settings.Server) ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.EncryptedPassword) ||
            string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            return SmtpTestResult.Failure("SMTP settings incomplete (server/username/password/from required).");
        }
        if (string.IsNullOrWhiteSpace(toAddress))
            return SmtpTestResult.Failure("Recipient address required for the test send.");

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
                Subject = "DaftarX — SMTP test",
                Body = "If you can read this, your DaftarX SMTP configuration works.\n\n" +
                       "(لو وصلتك هذه الرسالة، فإعدادات الـ SMTP في DaftarX تعمل بنجاح.)",
                IsBodyHtml = false,
            };
            message.To.Add(toAddress);

            await client.SendMailAsync(message, ct);
            return SmtpTestResult.Success($"Test email sent to {toAddress}.");
        }
        catch (SmtpException ex)
        {
            // Prefix with the SMTP status code so support sees what
            // the server actually said.
            return SmtpTestResult.Failure($"SMTP error ({ex.StatusCode}): {ex.Message}");
        }
        catch (Exception ex)
        {
            return SmtpTestResult.Failure(ex.Message);
        }
    }
}

public sealed record SmtpTestResult(bool Ok, string Message)
{
    public static SmtpTestResult Success(string msg) => new(true, msg);
    public static SmtpTestResult Failure(string msg) => new(false, msg);
}
