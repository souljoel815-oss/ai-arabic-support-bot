namespace EgyptTax.Application.Whatsapp;

/// <summary>
/// G2.2 — single port for WhatsApp dispatches. Two implementations
/// shipped:
///   * <c>MockWhatsAppDispatcher</c> — writes the audit row + returns
///     "Sent" without hitting any external API. Used until the
///     vendor registers a Meta WhatsApp Business number.
///   * <c>MetaCloudWhatsAppDispatcher</c> — calls the real Meta
///     Cloud API once configured (env vars: META_WHATSAPP_PHONE_ID,
///     META_WHATSAPP_ACCESS_TOKEN). Stub today; one-day swap.
///
/// The handler is responsible for writing the
/// <see cref="EgyptTax.Domain.Invoices.InvoiceWhatsAppDispatch"/>
/// audit row before returning. Caller does not need to track
/// state; the row is always written.
/// </summary>
public interface IWhatsAppDispatcher
{
    Task<WhatsAppDispatchResult> DispatchAsync(
        WhatsAppDispatchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WhatsAppDispatchRequest(
    Guid SalesInvoiceId,
    string RecipientPhone,
    string MessageBody,
    byte[] PdfBytes,
    string PdfFileName,
    Guid SentByUserId);

public sealed record WhatsAppDispatchResult(
    bool Success,
    Guid DispatchRecordId,
    string? ProviderMessageId,
    string? ErrorMessage);
