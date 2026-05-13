namespace EgyptTax.Domain.Settings;

/// <summary>
/// M.2 (v3 roadmap) — append-only audit log of every NL chat call
/// from the floating "اسأل دفترك" sidebar. Stores the user's
/// question, Claude's raw response, the parsed reply + suggested
/// deep-link URL, token counts, and which user asked.
///
/// Powers the v4 fine-tune corpus and also surfaces the "recent
/// chats" debug view in AI settings so the operator can see what
/// the AI was being asked.
/// </summary>
public sealed class AiChatLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime AskedAtUtc { get; init; }
    public Guid? AskedByUserId { get; init; }

    /// <summary>The user's typed question. Capped at 1000 chars.</summary>
    public string Question { get; init; } = "";

    /// <summary>Claude's raw response JSON. Capped at 4000.</summary>
    public string RawResponseJson { get; init; } = "";

    /// <summary>Parsed Arabic reply. Null when the parse failed.</summary>
    public string? Reply { get; init; }
    /// <summary>Parsed deep-link URL Claude suggested. Null when no nav suggested.</summary>
    public string? OpenUrl { get; init; }
    /// <summary>Parsed label for the "open" button.</summary>
    public string? OpenLabel { get; init; }

    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string? ErrorMessage { get; init; }

    private AiChatLog() { }

    public AiChatLog(
        DateTime askedAtUtc,
        Guid? askedByUserId,
        string question,
        string rawResponseJson,
        string? reply,
        string? openUrl,
        string? openLabel,
        int inputTokens,
        int outputTokens,
        string? errorMessage = null)
    {
        AskedAtUtc = askedAtUtc;
        AskedByUserId = askedByUserId;
        Question = (question ?? "").Length > 1000 ? question![..1000] : question ?? "";
        RawResponseJson = (rawResponseJson ?? "").Length > 3_900
            ? rawResponseJson![..3_900] + "…[truncated]"
            : rawResponseJson ?? "";
        Reply = reply;
        OpenUrl = openUrl;
        OpenLabel = openLabel;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        ErrorMessage = errorMessage;
    }
}
