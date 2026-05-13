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

    /// <summary>Off by default — operator must paste an Anthropic API key + flip on.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Ciphertext blob from IDataProtector. Never plaintext.</summary>
    public string? EncryptedApiKey { get; private set; }

    /// <summary>
    /// Claude model to use for both vision (M.1) and chat (M.2).
    /// Default: claude-haiku-4-5 — best price/quality for receipt
    /// OCR + short-form Arabic NL queries; Sonnet/Opus are
    /// over-spec for these tasks.
    /// </summary>
    public string ModelName { get; private set; } = "claude-haiku-4-5-20251001";

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

    public void Disable()
    {
        Enabled = false;
        // Don't wipe the key — operator may toggle back on.
    }

    public void UpdateModelOnly(string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ModelName = modelName.Trim();
    }
}
