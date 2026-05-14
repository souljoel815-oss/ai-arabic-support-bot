using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.Api;

/// <summary>
/// v4 B.3 — fires outbound HTTP POSTs to every enabled
/// <see cref="Webhook"/> whose <see cref="Webhook.EventMask"/>
/// matches the event name. Each POST carries:
///
///   * Content-Type: application/json
///   * X-Daftarx-Event:    {event-name}
///   * X-Daftarx-Signature: sha256={hex of HMAC-SHA256(secret, body)}
///
/// Best-effort delivery: 3 attempts with exponential backoff
/// (250ms / 500ms / 1000ms). Persistent failures are logged + the
/// status text is recorded on the webhook row; the originating
/// handler never observes the failure (webhooks are advisory).
/// Runs the dispatch on a background <c>Task.Run</c> so the inline
/// caller (the SalesInvoice post handler, etc.) doesn't pay the
/// HTTP latency.
/// </summary>
public sealed class WebhookDispatcher
{
    private static readonly TimeSpan[] BackoffDelays =
        { TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) };

    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebhookDispatcher> _log;

    public WebhookDispatcher(
        IServiceScopeFactory scopes,
        IHttpClientFactory httpFactory,
        ILogger<WebhookDispatcher> log)
    {
        _scopes = scopes;
        _httpFactory = httpFactory;
        _log = log;
    }

    /// <summary>Fire-and-forget the event. Returns immediately;
    /// dispatch happens on a background Task.</summary>
    public void Enqueue(string eventName, object payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(payload);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        // Background-fire so the caller's inline path isn't slowed
        // by HTTP latency. Errors land in the logger, not back in
        // the caller's exception path.
        _ = Task.Run(() => DispatchAsync(eventName, json));
    }

    private async Task DispatchAsync(string eventName, string json)
    {
        try
        {
            // Spin up our own DI scope: webhooks fire from background
            // tasks that may outlive the request scope of the caller
            // (the post handler). Resolving the AppDbContext from a
            // fresh scope avoids "DbContext disposed" races.
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hooks = await db.Set<Webhook>()
                .Where(w => w.Enabled)
                .ToListAsync();

            var matching = hooks.Where(h => h.MatchesEvent(eventName)).ToList();
            if (matching.Count == 0) return;

            using var http = _httpFactory.CreateClient("WebhookDispatcher");
            http.Timeout = TimeSpan.FromSeconds(15);

            foreach (var hook in matching)
            {
                var (success, status) = await PostWithRetryAsync(http, hook, eventName, json);
                hook.RecordDispatch(DateTime.UtcNow, status);
                if (success)
                {
                    _log.LogInformation(
                        "Webhook {Hook} fired for {Event}: {Status}",
                        hook.Name, eventName, status);
                }
                else
                {
                    _log.LogWarning(
                        "Webhook {Hook} failed for {Event} after retries: {Status}",
                        hook.Name, eventName, status);
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Last-ditch: dispatcher itself blew up (e.g., DB
            // unreachable). Webhooks are advisory; don't surface.
            _log.LogError(ex, "WebhookDispatcher.DispatchAsync failed.");
        }
    }

    private static async Task<(bool Success, string Status)> PostWithRetryAsync(
        HttpClient http,
        Webhook hook,
        string eventName,
        string json)
    {
        var signatureHex = HmacSha256Hex(hook.Secret, json);

        Exception? lastEx = null;
        string lastStatus = "no-attempt";
        for (var attempt = 0; attempt < BackoffDelays.Length + 1; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(BackoffDelays[attempt - 1]);
            }
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, hook.Url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                request.Headers.TryAddWithoutValidation("X-Daftarx-Event", eventName);
                request.Headers.TryAddWithoutValidation(
                    "X-Daftarx-Signature", $"sha256={signatureHex}");
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DaftarX-Webhook", "1.0"));

                using var response = await http.SendAsync(request);
                lastStatus = $"HTTP {(int)response.StatusCode}";
                if (response.IsSuccessStatusCode)
                {
                    return (true, lastStatus);
                }
                // 4xx other than 408/429 won't fix on retry; abort.
                var code = (int)response.StatusCode;
                if (code is >= 400 and < 500 and not 408 and not 429)
                {
                    return (false, lastStatus);
                }
            }
            catch (Exception ex)
            {
                lastEx = ex;
                lastStatus = $"exception: {ex.GetType().Name}";
            }
        }
        return (false, lastEx?.Message ?? lastStatus);
    }

    private static string HmacSha256Hex(string secret, string body)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = h.ComputeHash(Encoding.UTF8.GetBytes(body));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
