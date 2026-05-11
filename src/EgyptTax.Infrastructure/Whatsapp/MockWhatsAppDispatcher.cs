using EgyptTax.Application.Whatsapp;
using EgyptTax.Domain.Invoices;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.Whatsapp;

/// <summary>
/// G2.2 — default WhatsApp dispatcher used until the vendor wires
/// a Meta WhatsApp Business number. Writes the audit row, logs
/// the would-have-sent payload, returns "Sent" status with a
/// synthetic provider id so the dashboard / detail page treat it
/// as successful.
///
/// Swap to <c>MetaCloudWhatsAppDispatcher</c> by replacing the
/// DI registration in <c>Program.cs</c>; no caller changes
/// required (both implementations honour the same contract).
/// </summary>
public sealed class MockWhatsAppDispatcher : IWhatsAppDispatcher
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<MockWhatsAppDispatcher> _logger;

    public MockWhatsAppDispatcher(AppDbContext db, IClock clock, ILogger<MockWhatsAppDispatcher> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<WhatsAppDispatchResult> DispatchAsync(
        WhatsAppDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nowUtc = _clock.UtcNow;
        var providerId = $"mock-{Guid.NewGuid():N}";

        var record = new InvoiceWhatsAppDispatch(
            salesInvoiceId: request.SalesInvoiceId,
            recipientPhone: request.RecipientPhone,
            messageBody: request.MessageBody,
            sentAtUtc: nowUtc,
            sentByUserId: request.SentByUserId,
            providerMessageId: providerId);

        _db.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "MockWhatsAppDispatcher: would send invoice {InvoiceId} to {Phone} ({PdfBytes} bytes). Mock provider id {ProviderId}.",
            request.SalesInvoiceId, request.RecipientPhone, request.PdfBytes.Length, providerId);

        return new WhatsAppDispatchResult(
            Success: true,
            DispatchRecordId: record.Id,
            ProviderMessageId: providerId,
            ErrorMessage: null);
    }
}
