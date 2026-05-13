namespace EgyptTax.Domain.Settings;

/// <summary>
/// L8 (v3 roadmap) — per-customer-per-send log entry written by the
/// daily payment-reminder Hangfire job. Used to enforce the
/// "don't spam — 7-day cooldown per customer" rule and to surface
/// the reminder history on the Customer Statement page later.
///
/// Append-only: the job inserts; nothing updates or deletes.
/// </summary>
public sealed class PaymentReminderDispatch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public DateTime SentAtUtc { get; init; }
    /// <summary>Outstanding receivable balance at the moment the
    /// reminder was sent — recorded for the Customer Statement
    /// "you nudged them when they owed X" timeline view.</summary>
    public decimal OutstandingAtSendEgp { get; init; }
    public DateOnly OldestUnpaidInvoiceDate { get; init; }
    /// <summary>Email address the reminder was sent to (snapshot;
    /// the customer.Email value at send time).</summary>
    public string SentToEmail { get; init; } = "";

    private PaymentReminderDispatch() { }

    public PaymentReminderDispatch(
        Guid customerId,
        DateTime sentAtUtc,
        decimal outstandingAtSendEgp,
        DateOnly oldestUnpaidInvoiceDate,
        string sentToEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sentToEmail);
        CustomerId = customerId;
        SentAtUtc = sentAtUtc;
        OutstandingAtSendEgp = outstandingAtSendEgp;
        OldestUnpaidInvoiceDate = oldestUnpaidInvoiceDate;
        SentToEmail = sentToEmail.Trim();
    }
}
