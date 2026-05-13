using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using EgyptTax.Domain.Settings;

namespace EgyptTax.Infrastructure.Settings;

/// <summary>
/// L1 (v3 roadmap) — email a posted invoice's PDF to the customer.
/// Uses the existing SmtpSettings + SmtpPasswordProtector wiring.
///
/// Mirrors <see cref="SmtpTestSender"/>'s shape — same friendly
/// result type, same direct-SMTP-only requirement (the OS
/// MailClient mode can't attach a runtime-generated PDF without a
/// MAPI bridge).
///
/// Body is bilingual (Arabic + English) — every Egyptian B2B
/// recipient understands at least one. Subject leads with the
/// document number so the customer can sort their inbox by it.
/// </summary>
public sealed class SendInvoiceByEmailHandler
{
    private readonly SmtpPasswordProtector _protector;

    public SendInvoiceByEmailHandler(SmtpPasswordProtector protector) => _protector = protector;

    public async Task<SmtpTestResult> SendAsync(
        SmtpSettings settings,
        string toAddress,
        string documentNumber,
        decimal grandTotalEgp,
        byte[] pdfBytes,
        CancellationToken ct = default)
    {
        if (settings.SendMethod != EmailSendMethod.DirectSmtp)
        {
            return SmtpTestResult.Failure(
                "Email send requires Direct-SMTP mode. Configure it under Settings → Email Settings.");
        }
        if (string.IsNullOrWhiteSpace(settings.Server) ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.EncryptedPassword) ||
            string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            return SmtpTestResult.Failure("SMTP settings incomplete (server/username/password/from required).");
        }
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return SmtpTestResult.Failure("Customer email is missing — set it on the customer record first.");
        }
        if (pdfBytes is null || pdfBytes.Length == 0)
        {
            return SmtpTestResult.Failure("Could not generate the invoice PDF.");
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

            var subject = $"فاتورة {documentNumber} — Invoice {documentNumber}";
            var body =
                $"السلام عليكم،\n" +
                $"مرفق فاتورة رقم {documentNumber}.\n" +
                $"الإجمالي: {grandTotalEgp:N2} ج.م.\n" +
                $"شكراً لتعاملكم معنا.\n\n" +
                $"---\n\n" +
                $"Hello,\n" +
                $"Please find attached invoice {documentNumber}.\n" +
                $"Total: {grandTotalEgp:N2} EGP.\n" +
                $"Thank you for your business.";

            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromAddress, settings.FromName ?? settings.FromAddress),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };
            message.To.Add(toAddress);

            using var pdfStream = new MemoryStream(pdfBytes);
            using var attachment = new Attachment(pdfStream, $"{documentNumber}.pdf", MediaTypeNames.Application.Pdf);
            message.Attachments.Add(attachment);

            await client.SendMailAsync(message, ct);
            return SmtpTestResult.Success($"Sent invoice {documentNumber} to {toAddress}.");
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
