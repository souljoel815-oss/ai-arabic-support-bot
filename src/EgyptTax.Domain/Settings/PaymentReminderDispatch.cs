namespace EgyptTax.Domain.Settings;

/// <summary>
/// L8 (v3 roadmap) + v4 B.2 — per-customer-per-send log entry
/// written by the daily payment-reminder Hangfire job. Each row
/// records WHICH tier was sent (Gentle / Firm / FinalNotice) so
/// the v4 multi-tier escalation can apply a per-tier cooldown +
/// pick the right next tier on subsequent runs.
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
    /// <summary>v4 B.2 — which escalation tier this dispatch
    /// represents. Pre-v4 rows backfill to <see cref="ReminderTier.Gentle"/>
    /// (the only behaviour L8 had); the migration sets the
    /// column default accordingly.</summary>
    public ReminderTier Tier { get; init; } = ReminderTier.Gentle;

    private PaymentReminderDispatch() { }

    public PaymentReminderDispatch(
        Guid customerId,
        DateTime sentAtUtc,
        decimal outstandingAtSendEgp,
        DateOnly oldestUnpaidInvoiceDate,
        string sentToEmail,
        ReminderTier tier = ReminderTier.Gentle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sentToEmail);
        CustomerId = customerId;
        SentAtUtc = sentAtUtc;
        OutstandingAtSendEgp = outstandingAtSendEgp;
        OldestUnpaidInvoiceDate = oldestUnpaidInvoiceDate;
        SentToEmail = sentToEmail.Trim();
        Tier = tier;
    }
}

/// <summary>v4 B.2 — escalation tier for an outbound payment
/// reminder. Tone differs per tier; the job picks the highest tier
/// the customer is currently due for + sends only that one (no
/// double-emailing). Numeric ordering matches escalation order.</summary>
public enum ReminderTier
{
    /// <summary>Soft "friendly reminder" tone — first nudge.</summary>
    Gentle = 0,
    /// <summary>Direct "this is now overdue, please action" tone.</summary>
    Firm = 1,
    /// <summary>"Final notice before further action" tone — last
    /// automated touch before the operator escalates manually.</summary>
    FinalNotice = 2,
}
