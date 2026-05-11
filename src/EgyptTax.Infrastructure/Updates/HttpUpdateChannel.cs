using System.Text.Json;
using EgyptTax.Application.Updates;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.Updates;

/// <summary>
/// G4.1 — HTTP-backed update channel. Targets a vendor-controlled
/// JSON manifest (default <c>https://daftarx.com/latest.json</c>;
/// override via <c>EGYPTTAX_UPDATE_MANIFEST_URL</c> env var). 5s
/// timeout — if the vendor's CDN is slow we just skip the check
/// and try again tomorrow.
/// </summary>
public sealed class HttpUpdateChannel : IUpdateChannel
{
    public const string DefaultManifestUrl = "https://daftarx.com/latest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly Uri _manifestUrl;
    private readonly ILogger<HttpUpdateChannel> _logger;

    public HttpUpdateChannel(HttpClient http, ILogger<HttpUpdateChannel> logger)
    {
        _http = http;
        _logger = logger;
        var raw = Environment.GetEnvironmentVariable("EGYPTTAX_UPDATE_MANIFEST_URL");
        _manifestUrl = Uri.TryCreate(raw, UriKind.Absolute, out var parsed)
            ? parsed
            : new Uri(DefaultManifestUrl);
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<UpdateManifest?> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(_manifestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Update manifest check returned HTTP {Status} from {Url}",
                    (int)response.StatusCode, _manifestUrl);
                return null;
            }
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(
                stream, JsonOptions, cancellationToken);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex,
                "Update manifest check failed (will retry tomorrow) — {Url}",
                _manifestUrl);
            throw;
        }
    }
}
