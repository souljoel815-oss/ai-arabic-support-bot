namespace EgyptTax.Domain.Invoices;

/// <summary>
/// G2.2 — one row per WhatsApp dispatch attempt for a posted
/// invoice. The Cloud-API call goes through
/// <c>IWhatsAppDispatcher</c>; that handler writes one of these
/// rows whether the send succeeded or failed.
///
/// Why a separate entity (not just a column on SalesInvoice):
///   * One invoice can be sent multiple times (corrections,
///     re-sends after the customer says "didn't get it").
///   * Each dispatch has its own outcome, message body (templates
///     can change between attempts), recipient phone (operator
///     might fix a typo on the second try), and Meta message-id
///     for read-receipt correlation.
///   * Audit / investigation needs the full history: a regulator
///     asking "when did the customer receive their invoice?"
///     wants every attempt visible.
///
/// FK: <see cref="SalesInvoiceId"/> → SalesInvoice.Id. Append-only;
/// no edits after the row is written. Mark-delivered / mark-read
/// are mutations of <see cref="DeliveryStatus"/> via the dedicated
/// methods (no ad-hoc state changes).
/// </summary>
public sealed class InvoiceWhatsAppDispatch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SalesInvoiceId { get; init; }
    public string RecipientPhone { get; init; } = "";
    public string MessageBody { get; init; } = "";
    public DateTime SentAtUtc { get; init; }
    public Guid SentByUserId { get; init; }

    public WhatsAppDispatchStatus DeliveryStatus { get; private set; }
        = WhatsAppDispatchStatus.Sent;

    /// <summary>Meta-issued message id (wamid). Used to correlate
    /// the send with later delivery + read webhooks. Empty when
    /// the dispatcher is the mock / no Meta integration yet.</summary>
    public string? ProviderMessageId { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    private InvoiceWhatsAppDispatch() { }

    public InvoiceWhatsAppDispatch(
        Guid salesInvoiceId,
        string recipientPhone,
        string messageBody,
        DateTime sentAtUtc,
        Guid sentByUserId,
        string? providerMessageId = null)
    {
        if (salesInvoiceId == Guid.Empty)
            throw new ArgumentException("SalesInvoiceId is required.", nameof(salesInvoiceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageBody);
        if (sentByUserId == Guid.Empty)
            throw new ArgumentException("SentByUserId is required.", nameof(sentByUserId));

        SalesInvoiceId = salesInvoiceId;
        RecipientPhone = recipientPhone.Trim();
        MessageBody = messageBody.Trim();
        SentAtUtc = sentAtUtc;
        SentByUserId = sentByUserId;
        ProviderMessageId = string.IsNullOrWhiteSpace(providerMessageId) ? null : providerMessageId.Trim();
    }

    /// <summary>Webhook handler calls this when Meta confirms
    /// delivery. Idempotent.</summary>
    public void MarkDelivered(DateTime deliveredAtUtc)
    {
        if (DeliveryStatus == WhatsAppDispatchStatus.Failed) return;
        DeliveredAtUtc ??= deliveredAtUtc;
        if (DeliveryStatus == WhatsAppDispatchStatus.Sent)
        {
            DeliveryStatus = WhatsAppDispatchStatus.Delivered;
        }
    }

    /// <summary>Webhook handler calls this when Meta confirms
    /// the customer opened the message. Idempotent.</summary>
    public void MarkRead(DateTime readAtUtc)
    {
        if (DeliveryStatus == WhatsAppDispatchStatus.Failed) return;
        ReadAtUtc ??= readAtUtc;
        DeliveryStatus = WhatsAppDispatchStatus.Read;
    }

    public void MarkFailed(DateTime failedAtUtc, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (DeliveryStatus == WhatsAppDispatchStatus.Read) return;
        FailedAtUtc ??= failedAtUtc;
        FailureReason ??= reason.Trim();
        DeliveryStatus = WhatsAppDispatchStatus.Failed;
    }
}

public enum WhatsAppDispatchStatus
{
    /// <summary>Server accepted the request; awaiting Meta delivery
    /// confirmation. Mock dispatcher ends here.</summary>
    Sent,
    /// <summary>Meta confirmed the customer's device received it.</summary>
    Delivered,
    /// <summary>Customer opened the message.</summary>
    Read,
    /// <summary>Send failed (invalid number, Meta API error,
    /// rate limit, etc.). <c>FailureReason</c> carries the detail.</summary>
    Failed,
}
