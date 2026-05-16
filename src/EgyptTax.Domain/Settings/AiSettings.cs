namespace EgyptTax.Domain.Settings;

/// <summary>
/// M-phase (v3 roadmap) — Anthropic Claude API configuration. Same
/// single-row pattern as <see cref="SmtpSettings"/>. The API key
/// is encrypted via ASP.NET Data Protection on write and decrypted
/// only inside the Anthropic HTTP client.
///
/// MonthlyTokenBudget is informational in v1 — the M.1/M.2 code
/// records actual token usage on every call into a sibling
/// <c>AiUsageLedger</c> entity (deferred — we just log totals to
/// ReceiptScan for now), and the admin panel surfaces the running
/// total so the operator can see whether they're tracking close to
/// the cap. Hard-blocking at the cap lands when a customer
/// actually overshoots.
/// </summary>
public sealed class AiSettings
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Off by default — operator must paste an API key + flip on.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Ciphertext blob from IDataProtector. Never plaintext.
    /// Used for the <see cref="ChatProvider"/>'s API key (Anthropic OR Groq).</summary>
    public string? EncryptedApiKey { get; private set; }

    /// <summary>
    /// Claude model used for OCR (M.1 — vision) and, when
    /// <see cref="ChatProvider"/> == Anthropic, also for the NL
    /// chat queries (M.2). Default: claude-haiku-4-5.
    /// </summary>
    public string ModelName { get; private set; } = "claude-haiku-4-5-20251001";

    /// <summary>v5 — provider for the NL chat queries (M.2). OCR
    /// always uses Anthropic because Groq's Llama 4 Scout is text-only;
    /// when the operator picks Groq for chat, the OCR path falls back
    /// to the legacy Anthropic key if also configured.</summary>
    public AiChatProvider ChatProvider { get; private set; } = AiChatProvider.Anthropic;

    /// <summary>v5 — Groq chat model id (e.g. "meta-llama/llama-4-scout-17b-16e-instruct").
    /// Ignored when <see cref="ChatProvider"/> == Anthropic.</summary>
    public string GroqModelName { get; private set; } = "meta-llama/llama-4-scout-17b-16e-instruct";

    /// <summary>v5 — separate encrypted key for Groq, kept distinct from
    /// <see cref="EncryptedApiKey"/> so swapping providers doesn't wipe
    /// the other vendor's key. Operator can paste either + toggle.</summary>
    public string? EncryptedGroqApiKey { get; private set; }

    /// <summary>Soft cap (informational; not enforced in v1). EGP/month estimated cost.</summary>
    public int MonthlyBudgetEgp { get; private set; } = 500;

    private AiSettings() { }

    public static AiSettings CreateDefault() => new();

    public void Configure(string encryptedApiKey, string modelName, int monthlyBudgetEgp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedApiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyBudgetEgp);
        EncryptedApiKey = encryptedApiKey;
        ModelName = modelName.Trim();
        MonthlyBudgetEgp = monthlyBudgetEgp;
        Enabled = true;
    }

    /// <summary>v5 — configure the Groq chat provider. Separate key
    /// from Anthropic so both can coexist (OCR on Anthropic, chat on
    /// Groq). Pass <paramref name="encryptedKey"/> = null/empty to
    /// keep the existing stored key (re-saving without re-typing).</summary>
    public void ConfigureGroq(string? encryptedKey, string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        if (!string.IsNullOrWhiteSpace(encryptedKey))
        {
            EncryptedGroqApiKey = encryptedKey;
        }
        GroqModelName = modelName.Trim();
        ChatProvider = AiChatProvider.Groq;
        Enabled = true;
    }

    /// <summary>v5 — switch chat back to Anthropic without wiping the
    /// stored Groq key (operator can flip back later).</summary>
    public void SwitchChatProvider(AiChatProvider provider) => ChatProvider = provider;

    public void Disable()
    {
        Enabled = false;
        // Don't wipe keys — operator may toggle back on.
    }

    public void UpdateModelOnly(string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ModelName = modelName.Trim();
    }
}

public enum AiChatProvider
{
    Anthropic,
    Groq,
}
