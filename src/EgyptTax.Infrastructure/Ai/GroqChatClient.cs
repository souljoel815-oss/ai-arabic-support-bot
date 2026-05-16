using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgyptTax.Infrastructure.Ai;

/// <summary>
/// v5 — Groq client for both chat (M.2 NL queries) and vision (M.1
/// receipt OCR). Talks to Groq's OpenAI-compatible
/// <c>chat/completions</c> endpoint. Llama 4 Scout is multimodal
/// (text + image), so the same model id handles both paths.
///
/// The chat / vision method shapes mirror <c>AnthropicVisionClient</c>
/// so handlers route by <see cref="EgyptTax.Domain.Settings.AiChatProvider"/>
/// without changing their call signatures.
/// </summary>
public sealed class GroqChatClient
{
    private const string ApiBaseUrl = "https://api.groq.com/openai/v1/chat/completions";

    private readonly HttpClient _http;

    public GroqChatClient(HttpClient http) => _http = http;

    public async Task<ChatResult> SendChatMessageAsync(
        string apiKey,
        string modelName,
        string systemPrompt,
        string userMessage,
        int maxTokens = 1024,
        IEnumerable<(string Role, string Content)>? priorMessages = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var msgs = new List<ChatMessage>
        {
            new() { Role = "system", Content = JsonValue.FromString(systemPrompt) },
        };
        if (priorMessages is not null)
        {
            foreach (var (role, content) in priorMessages)
            {
                if (string.IsNullOrWhiteSpace(content)) continue;
                msgs.Add(new ChatMessage { Role = role, Content = JsonValue.FromString(content) });
            }
        }
        msgs.Add(new ChatMessage { Role = "user", Content = JsonValue.FromString(userMessage) });

        var payload = new ChatRequest
        {
            Model = modelName,
            MaxTokens = maxTokens,
            Messages = msgs.ToArray(),
        };
        return await SendAsync(apiKey, payload, ct);
    }

    /// <summary>v5 — tool-calling-aware send. Returns the message
    /// the model produced (either a final assistant content string
    /// or a list of tool_calls to execute). Caller is responsible
    /// for running the loop. <paramref name="thread"/> is the running
    /// conversation including system + priors + user + any assistant
    /// + tool messages produced so far.</summary>
    public async Task<ToolAwareResult> SendChatThreadAsync(
        string apiKey,
        string modelName,
        IReadOnlyList<ToolThreadMessage> thread,
        IReadOnlyList<object>? tools = null,
        int maxTokens = 1024,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentNullException.ThrowIfNull(thread);

        var msgs = thread.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = JsonValue.FromString(m.Content ?? ""),
            ToolCallId = m.ToolCallId,
            ToolCalls = m.ToolCalls,
        }).ToArray();

        var payload = new ChatRequest
        {
            Model = modelName,
            MaxTokens = maxTokens,
            Messages = msgs,
            Tools = tools is { Count: > 0 } ? tools.ToArray() : null,
            ToolChoice = tools is { Count: > 0 } ? "auto" : null,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Groq API returned {(int)response.StatusCode}: {body}");
        }
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var msg = root.TryGetProperty("choices", out var choices)
                  && choices.ValueKind == JsonValueKind.Array
                  && choices.GetArrayLength() > 0
            ? choices[0].GetProperty("message")
            : default;

        var content = msg.ValueKind == JsonValueKind.Object
            && msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() ?? ""
            : "";

        var toolCalls = new List<ToolCall>();
        if (msg.ValueKind == JsonValueKind.Object
            && msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in tc.EnumerateArray())
            {
                var id = call.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var fn = call.TryGetProperty("function", out var fnEl) ? fnEl : default;
                var name = fn.ValueKind == JsonValueKind.Object
                    && fn.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                var args = fn.ValueKind == JsonValueKind.Object
                    && fn.TryGetProperty("arguments", out var ag) ? ag.GetString() ?? "{}" : "{}";
                toolCalls.Add(new ToolCall(id, name, args));
            }
        }

        int inTok = 0, outTok = 0;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number)
                inTok = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct2) && ct2.ValueKind == JsonValueKind.Number)
                outTok = ct2.GetInt32();
        }

        return new ToolAwareResult(content, toolCalls, body, inTok, outTok);
    }

    /// <summary>v5 — multimodal call. The OpenAI-compatible payload
    /// takes <c>content</c> as an array of text + image_url parts when
    /// images are involved. Llama 4 Scout decodes a base64 data URL
    /// directly. Used by <c>OcrReceiptHandler</c> when ChatProvider = Groq.</summary>
    public async Task<ChatResult> SendVisionMessageAsync(
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

        // Groq Vision (Llama 4 Scout/Maverick) supports JPEG, PNG, GIF,
        // WebP — NOT PDF, NOT HEIC. Reject up-front with a clear
        // operator-facing message instead of a cryptic "invalid image
        // data" from the upstream API.
        var normalizedMime = NormalizeMimeType(imageMimeType);
        if (normalizedMime is null)
        {
            throw new InvalidOperationException(
                $"Groq vision doesn't accept '{imageMimeType}'. Upload JPG, PNG, GIF, or WebP "
                + "(PDFs and HEIC are not supported by Llama 4 Scout — convert first or use Anthropic).");
        }

        // Groq's hard cap for base64 images is 4 MB encoded. Base64
        // inflates by ~33%, so the raw bytes ceiling is ~3 MB.
        const long MaxRawBytes = 3 * 1024 * 1024;
        if (imageBytes.LongLength > MaxRawBytes)
        {
            throw new InvalidOperationException(
                $"Image is {imageBytes.LongLength / 1024 / 1024} MB; Groq Vision caps base64 uploads "
                + "at ~3 MB raw (~4 MB encoded). Resize or use a smaller capture.");
        }

        var base64Image = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{normalizedMime};base64,{base64Image}";

        var parts = new ContentPart[]
        {
            new() { Type = "text", Text = textPrompt },
            new() { Type = "image_url", ImageUrl = new ImageUrlBlock { Url = dataUrl } },
        };
        var payload = new ChatRequest
        {
            Model = modelName,
            MaxTokens = maxTokens,
            Messages = new[]
            {
                new ChatMessage
                {
                    Role = "user",
                    Content = JsonValue.FromParts(parts),
                },
            },
        };
        return await SendAsync(apiKey, payload, ct);
    }

    private async Task<ChatResult> SendAsync(string apiKey, ChatRequest payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl)
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Groq API returned {(int)response.StatusCode}: {body}");
        }

        var parsed = JsonSerializer.Deserialize<ChatResponse>(body)
            ?? throw new InvalidOperationException("Groq API returned an empty body.");

        var assistantText = parsed.Choices?.FirstOrDefault()?.Message?.Content ?? "";
        return new ChatResult(
            AssistantText: assistantText,
            RawResponseJson: body,
            InputTokens: parsed.Usage?.PromptTokens ?? 0,
            OutputTokens: parsed.Usage?.CompletionTokens ?? 0);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Maps incoming mime types to the exact strings Groq's
    /// vision endpoint accepts. Returns null for unsupported types
    /// (PDF, HEIC, etc.) so the caller can throw a clear error.</summary>
    private static string? NormalizeMimeType(string mime)
    {
#pragma warning disable CA1308 // MIME types are matched lowercase per RFC 2046; lowercase normalization is correct here.
        var m = mime.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        // Common aliases (e.g. browsers occasionally emit "image/jpg").
        return m switch
        {
            "image/jpeg" or "image/jpg" or "image/pjpeg" => "image/jpeg",
            "image/png" => "image/png",
            "image/gif" => "image/gif",
            "image/webp" => "image/webp",
            _ => null,
        };
    }

    public sealed record ChatResult(
        string AssistantText,
        string RawResponseJson,
        int InputTokens,
        int OutputTokens);

    /// <summary>v5 — result shape for the tool-aware path. Either
    /// <see cref="ToolCalls"/> is non-empty (model wants the caller
    /// to run tools and re-ask) OR <see cref="AssistantText"/> is the
    /// final reply. They CAN both be non-empty on some completions
    /// — callers should prefer tool execution when present and only
    /// treat content as final when tool_calls is empty.</summary>
    public sealed record ToolAwareResult(
        string AssistantText,
        IReadOnlyList<ToolCall> ToolCalls,
        string RawResponseJson,
        int InputTokens,
        int OutputTokens);

    public sealed record ToolCall(string Id, string FunctionName, string ArgumentsJson);

    /// <summary>v5 — message shape passed into <see cref="SendChatThreadAsync"/>.
    /// Mirrors OpenAI's role+content+tool_call_id+tool_calls union.
    /// For role="tool" set <see cref="ToolCallId"/> and put the JSON
    /// result in <see cref="Content"/>. For an assistant message that
    /// requested tools, set <see cref="ToolCalls"/> (and leave content
    /// empty).</summary>
    public sealed record ToolThreadMessage(
        string Role,
        string? Content,
        string? ToolCallId = null,
        object[]? ToolCalls = null);

    /// <summary>OpenAI's chat API accepts the <c>content</c> field as
    /// either a plain string OR an array of typed parts. .NET's
    /// JsonSerializer doesn't speak union types natively, so we wrap
    /// the value in a custom <see cref="JsonValue"/> that hand-renders
    /// the correct shape on write via a converter.</summary>
    [JsonConverter(typeof(JsonValueConverter))]
    private sealed class JsonValue
    {
        public string? Str { get; set; }
        public ContentPart[]? Parts { get; set; }
        public static JsonValue FromString(string s) => new() { Str = s };
        public static JsonValue FromParts(ContentPart[] p) => new() { Parts = p };
    }

    private sealed class JsonValueConverter : JsonConverter<JsonValue>
    {
        public override JsonValue? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions opts) =>
            throw new NotSupportedException("JsonValue is write-only (request payload).");

        public override void Write(Utf8JsonWriter writer, JsonValue value, JsonSerializerOptions opts)
        {
            if (value.Parts is { } parts)
            {
                JsonSerializer.Serialize(writer, parts, opts);
            }
            else
            {
                writer.WriteStringValue(value.Str ?? "");
            }
        }
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("messages")] public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.2;
        [JsonPropertyName("tools"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object[]? Tools { get; set; }
        [JsonPropertyName("tool_choice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolChoice { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public JsonValue Content { get; set; } = JsonValue.FromString("");
        [JsonPropertyName("tool_call_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }
        [JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object[]? ToolCalls { get; set; }
    }

    private sealed class ContentPart
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }
        [JsonPropertyName("image_url"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImageUrlBlock? ImageUrl { get; set; }
    }

    private sealed class ImageUrlBlock
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public Choice[]? Choices { get; set; }
        [JsonPropertyName("usage")] public UsageBlock? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public ResponseMessage? Message { get; set; }
    }

    private sealed class ResponseMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class UsageBlock
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    }
}
