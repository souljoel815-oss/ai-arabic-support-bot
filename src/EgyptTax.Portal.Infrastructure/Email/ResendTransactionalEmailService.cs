using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EgyptTax.Portal.Application.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgyptTax.Portal.Infrastructure.Email;

/// <summary>
/// T031 + T095 per research §4. Production transactional email via the
/// Resend.com HTTPS API. Polly retry is wired in <c>Program.cs</c> via
/// <c>AddHttpClient&lt;ResendTransactionalEmailService&gt;().AddTransientHttpErrorPolicy(...)</c>
/// to handle transient 5xx + 429s without throwing into the caller. The full
/// MJML template renderer lands in T096; for now this adapter just posts the
/// caller-provided HTML body.
/// </summary>
internal sealed class ResendTransactionalEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendTransactionalEmailService> _logger;

    public ResendTransactionalEmailService(
        HttpClient http,
        IOptions<ResendOptions> options,
        ILogger<ResendTransactionalEmailService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var fromName = message.FromName ?? _options.FromName;
        var payload = new ResendSendRequest(
            From: $"{fromName} <{_options.FromAddress}>",
            To: new[] { message.ToAddress },
            Subject: message.Subject,
            Html: message.HtmlBody,
            Text: message.TextBody,
            Attachments: message.Attachments?.Select(a => new ResendAttachment(
                Filename: a.FileName,
                Content: Convert.ToBase64String(a.Content),
                ContentType: a.MediaType)).ToArray());

        using var response = await _http
            .PostAsJsonAsync("emails", payload, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Resend rejected email to {To} subject {Subject}: HTTP {Status} — {Body}",
                message.ToAddress,
                message.Subject,
                (int)response.StatusCode,
                body);
            response.EnsureSuccessStatusCode();
        }
    }

    private sealed record ResendSendRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("attachments")] ResendAttachment[]? Attachments);

    private sealed record ResendAttachment(
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("content_type")] string ContentType);
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "noreply@daftarx.app";

    public string FromName { get; set; } = "DaftarX";

    public string BaseUrl { get; set; } = "https://api.resend.com/";

    /// <summary>Set to <c>"Stdout"</c> in dev to route via <see cref="DevStdoutEmailService"/>.</summary>
    public string Mode { get; set; } = "Resend";
}
