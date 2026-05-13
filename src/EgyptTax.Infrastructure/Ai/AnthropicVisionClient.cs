using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// M-phase — minimal HTTP client for the Anthropic Messages API
/// (Vision endpoint). Hand-rolled rather than depending on the
/// .NET Anthropic SDK so the portable single-file deployment
/// doesn't carry a transitive dep that could lag behind the API.
///
/// One method exposed: <see cref="SendVisionMessageAsync"/> — posts
/// a single user message containing one image + one text block,
/// returns the assistant's text response + token usage. Caller
/// (OcrReceiptHandler) parses the response.
/// </summary>
public sealed class AnthropicVisionClient
{
    private const string ApiBaseUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _http;

    public AnthropicVisionClient(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// M.2 — text-only chat call. Same /v1/messages endpoint but the
    /// content block carries only text (no image). System prompt is
    /// passed at the top level per the Anthropic API contract.
    /// </summary>
    public async Task<VisionResult> SendChatMessageAsync(
        string apiKey,
        string modelName,
        string systemPrompt,
        string userMessage,
        int maxTokens = 800,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var payload = new MessagesRequest
        {
            Model = modelName,
            MaxTokens = maxTokens,
            System = systemPrompt,
            Messages = new[]
            {
                new MessageRequest
                {
                    Role = "user",
                    Content = new ContentBlock[]
                    {
                        new() { Type = "text", Text = userMessage },
                    },
                },
            },
        };

        return await SendAsync(apiKey, payload, ct);
    }

    public async Task<VisionResult> SendVisionMessageAsync(
        string apiKey,
        string modelName,
        byte[] imageBytes,
        string imageMimeType,
        string textPrompt,
        int maxTokens = 1024,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageMimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(textPrompt);

        var base64Image = Convert.ToBase64String(imageBytes);
        var payload = new MessagesRequest
        {
            Model = modelName,
            MaxTokens = maxTokens,
            Messages = new[]
            {
                new MessageRequest
                {
                    Role = "user",
                    Content = new ContentBlock[]
                    {
                        new()
                        {
                            Type = "image",
                            Source = new ImageSource
                            {
                                Type = "base64",
                                MediaType = imageMimeType,
                                Data = base64Image,
                            },
                        },
                        new()
                        {
                            Type = "text",
                            Text = textPrompt,
                        },
                    },
                },
            },
        };

        return await SendAsync(apiKey, payload, ct);
    }

    private async Task<VisionResult> SendAsync(string apiKey, MessagesRequest payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Anthropic API returned {(int)response.StatusCode}: {body}");
        }

        var parsed = JsonSerializer.Deserialize<MessagesResponse>(body)
            ?? throw new InvalidOperationException("Anthropic API returned an empty body.");

        var assistantText = parsed.Content?.FirstOrDefault(c => c.Type == "text")?.Text ?? "";
        return new VisionResult(
            AssistantText: assistantText,
            RawResponseJson: body,
            InputTokens: parsed.Usage?.InputTokens ?? 0,
            OutputTokens: parsed.Usage?.OutputTokens ?? 0);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed record VisionResult(
        string AssistantText,
        string RawResponseJson,
        int InputTokens,
        int OutputTokens);

    private sealed class MessagesRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("system")] public string? System { get; set; }
        [JsonPropertyName("messages")] public MessageRequest[] Messages { get; set; } = Array.Empty<MessageRequest>();
    }

    private sealed class MessageRequest
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public ContentBlock[] Content { get; set; } = Array.Empty<ContentBlock>();
    }

    private sealed class ContentBlock
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("source")] public ImageSource? Source { get; set; }
    }

    private sealed class ImageSource
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("media_type")] public string MediaType { get; set; } = "";
        [JsonPropertyName("data")] public string Data { get; set; } = "";
    }

    private sealed class MessagesResponse
    {
        [JsonPropertyName("content")] public ResponseContent[]? Content { get; set; }
        [JsonPropertyName("usage")] public TokenUsage? Usage { get; set; }
    }

    private sealed class ResponseContent
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class TokenUsage
    {
        [JsonPropertyName("input_tokens")] public int InputTokens { get; set; }
        [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
    }
}
