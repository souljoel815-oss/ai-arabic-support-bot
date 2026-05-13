using System.Net;
using System.Net.Mail;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Settings;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Settings;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// L8 (v3 roadmap) — daily Hangfire job that sends polite payment-
/// reminder emails to customers whose oldest unpaid invoice is older
/// than the configured threshold (default 14 days).
///
/// Per-customer (NOT per-invoice) reminders: receipts are FIFO-
/// applied to oldest invoices to find the oldest still-unpaid one.
/// One email summarises all outstanding amounts. Avoids spamming
/// customers with five separate emails for five overdue invoices.
///
/// Anti-spam: a 7-day cooldown — won't re-send to the same customer
/// within 7 days of the previous dispatch (recorded in
/// <see cref="PaymentReminderDispatch"/>).
///
/// Skips silently when:
///   - PaymentReminderEnabled is false
///   - SMTP is not configured for DirectSmtp
///   - The customer has no email address on file
///   - The 7-day cooldown is still active
/// </summary>
public sealed class PaymentReminderJob
{
    private const int CooldownDays = 7;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly SettingsRepository _settings;
    private readonly SmtpPasswordProtector _protector;
    private readonly IClock _clock;
    private readonly ILogger<PaymentReminderJob> _log;

    public PaymentReminderJob(
        IDbContextFactory<AppDbContext> dbFactory,
        SettingsRepository settings,
        SmtpPasswordProtector protector,
        IClock clock,
        ILogger<PaymentReminderJob> log)
    {
        _dbFactory = dbFactory;
        _settings = settings;
        _protector = protector;
        _clock = clock;
        _log = log;
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        var prefs = await _settings.GetNotificationPrefsAsync(ct);
        if (!prefs.PaymentReminderEnabled)
        {
            _log.LogDebug("Payment reminders disabled; skipping.");
            return;
        }

        var smtp = await _settings.GetSmtpSettingsAsync(ct);
        if (smtp.SendMethod != EmailSendMethod.DirectSmtp ||
            string.IsNullOrWhiteSpace(smtp.Server) ||
            string.IsNullOrWhiteSpace(smtp.Username) ||
            string.IsNullOrWhiteSpace(smtp.EncryptedPassword) ||
            string.IsNullOrWhiteSpace(smtp.FromAddress))
        {
            _log.LogWarning("Payment reminders enabled but SMTP not configured; skipping.");
            return;
        }

        var threshold = prefs.PaymentReminderDaysOverdue;
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var cooldownCutoff = _clock.UtcNow.AddDays(-CooldownDays);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var customers = await db.Set<Customer>().AsNoTracking()
            .Where(c => c.Status == CustomerStatus.Active && c.Email != null && c.Email != "")
            .ToListAsync(ct);

        var sent = 0;
        var skippedNoOverdue = 0;
        var skippedCooldown = 0;
        var failed = 0;

        foreach (var customer in customers)
        {
            ct.ThrowIfCancellationRequested();

            // Cooldown check first (cheapest).
            var lastSent = await db.Set<PaymentReminderDispatch>().AsNoTracking()
                .Where(d => d.CustomerId == customer.Id && d.SentAtUtc >= cooldownCutoff)
                .OrderByDescending(d => d.SentAtUtc)
                .Select(d => (DateTime?)d.SentAtUtc)
                .FirstOrDefaultAsync(ct);
            if (lastSent is not null)
            {
                skippedCooldown++;
                continue;
            }

            var (oldestDate, outstanding) = await ComputeOldestUnpaidAsync(db, customer.Id, ct);
            if (oldestDate is null || outstanding <= 0m)
            {
                skippedNoOverdue++;
                continue;
            }

            var ageDays = today.DayNumber - oldestDate.Value.DayNumber;
            if (ageDays < threshold)
            {
                skippedNoOverdue++;
                continue;
            }

            try
            {
                await SendReminderEmailAsync(smtp, customer, outstanding, oldestDate.Value, ageDays, ct);

                db.Add(new PaymentReminderDispatch(
                    customerId: customer.Id,
                    sentAtUtc: _clock.UtcNow,
                    outstandingAtSendEgp: outstanding,
                    oldestUnpaidInvoiceDate: oldestDate.Value,
                    sentToEmail: customer.Email!));
                await db.SaveChangesAsync(ct);
                sent++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Payment-reminder email to {Email} for customer {Customer} failed.",
                    customer.Email, customer.Id);
                failed++;
            }
        }

        _log.LogInformation(
            "Payment reminders: sent={Sent} skippedNoOverdue={Skip1} skippedCooldown={Skip2} failed={Failed}",
            sent, skippedNoOverdue, skippedCooldown, failed);
    }

    /// <summary>
    /// FIFO-applies receipts to invoices ordered by document date and
    /// returns (date-of-oldest-still-unpaid-invoice, total-outstanding).
    /// Materialises the lists in memory because SQLite can't aggregate
    /// decimals server-side; volumes per customer are bounded.
    /// </summary>
    private static async Task<(DateOnly? OldestDate, decimal Outstanding)> ComputeOldestUnpaidAsync(
        AppDbContext db, Guid customerId, CancellationToken ct)
    {
        var invoiceRows = await db.Set<SalesInvoice>().AsNoTracking()
            .Where(i => i.CustomerId == customerId && i.State == DocumentState.Posted)
            .OrderBy(i => i.DocumentDate)
            .Select(i => new { i.DocumentDate, i.GrandTotal.Amount, IsCredit = i.CreditNoteOfInvoiceId != null })
            .ToListAsync(ct);

        var receiptTotal = (await db.Set<CustomerReceiptVoucher>().AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.State == DocumentState.Posted)
            .Select(r => r.GrossReceiptAmount.Amount)
            .ToListAsync(ct))
            .Sum();

        // Treat credit notes as direct reductions to the receivable
        // pool — same as the customer-statement page does.
        var creditNoteOffsets = invoiceRows
            .Where(r => r.IsCredit)
            .Sum(r => Math.Abs(r.Amount));
        var pool = receiptTotal + creditNoteOffsets;

        DateOnly? oldestStillOpen = null;
        decimal outstanding = 0m;
        foreach (var row in invoiceRows)
        {
            if (row.IsCredit) continue;
            var unpaid = row.Amount - Math.Min(row.Amount, pool);
            pool = Math.Max(0m, pool - row.Amount);
            if (unpaid > 0m)
            {
                oldestStillOpen ??= row.DocumentDate;
                outstanding += unpaid;
            }
        }

        return (oldestStillOpen, outstanding);
    }

    private async Task SendReminderEmailAsync(
        SmtpSettings smtp,
        Customer customer,
        decimal outstanding,
        DateOnly oldestDate,
        int ageDays,
        CancellationToken ct)
    {
        var password = _protector.Decrypt(smtp.EncryptedPassword!);
        using var client = new SmtpClient(smtp.Server!, smtp.Port)
        {
            EnableSsl = smtp.UseTls,
            Credentials = new NetworkCredential(smtp.Username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        var subjectAr = $"تذكير ودّي بالرصيد المستحق — {outstanding:N2} ج.م";
        var subjectEn = $"Friendly payment reminder — EGP {outstanding:N2} outstanding";

        var bodyAr =
            $"السيد / السيدة {customer.Name.Arabic},\n\n" +
            $"دي رسالة تذكيرية ودّية بالرصيد المستحق على حضرتكم:\n\n" +
            $"  • إجمالي الرصيد المستحق: {outstanding:N2} ج.م\n" +
            $"  • أقدم فاتورة غير مسددة: {oldestDate:yyyy-MM-dd} (مر عليها {ageDays} يوم)\n\n" +
            $"لو سبق التحويل، يرجى تجاهل الرسالة. للتواصل أو الاستفسار، رد على البريد ده مباشرةً.\n\n" +
            $"شكراً لتعاملكم.";

        var bodyEn =
            $"Dear {customer.Name.English},\n\n" +
            $"This is a friendly reminder regarding your outstanding balance:\n\n" +
            $"  • Total outstanding: EGP {outstanding:N2}\n" +
            $"  • Oldest unpaid invoice: {oldestDate:yyyy-MM-dd} ({ageDays} days ago)\n\n" +
            $"If payment has already been sent, please disregard this notice. " +
            $"For questions, reply directly to this email.\n\n" +
            $"Thank you for your business.";

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress!, smtp.FromName ?? smtp.FromAddress!),
            Subject = $"{subjectAr}  ·  {subjectEn}",
            Body = bodyAr + "\n\n— — —\n\n" + bodyEn,
            IsBodyHtml = false,
        };
        message.To.Add(customer.Email!);
        await client.SendMailAsync(message, ct);
    }
}
