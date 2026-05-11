namespace EgyptTax.Domain.Settings;

/// <summary>
/// Gux.13 Tab 4 — invoice numbering, formatting, and template
/// preferences. Single-row config (one per install).
///
/// Lazy-initialized: if no row exists when first accessed, the
/// repository creates a row with the defaults below. Pre-Gux.13
/// installs upgrading to v2 don't need a data migration step;
/// they get defaults on first access.
/// </summary>
public sealed class InvoiceSettings
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string TemplateId { get; private set; } = "classic";
    public InvoiceLanguageMode Language { get; private set; } = InvoiceLanguageMode.ArabicOnly;
    public string Prefix { get; private set; } = "INV-";
    public int NextNumber { get; private set; } = 1;
    public int DefaultPaymentTermsDays { get; private set; } = 30;
    public string? FooterNotes { get; private set; }
    public bool ShowQrCode { get; private set; } = true;
    public bool ShowLogo { get; private set; } = true;

    private InvoiceSettings() { }

    public static InvoiceSettings CreateDefault() => new();

    public void UpdateTemplate(string templateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        TemplateId = templateId.Trim();
    }

    public void UpdateLanguage(InvoiceLanguageMode mode) => Language = mode;

    public void UpdatePrefix(string prefix)
    {
        // Empty prefix is fine (some operators just want raw numbers).
        Prefix = prefix?.Trim() ?? "";
    }

    /// <summary>
    /// Tab 4 spec — Next Number override. Caller MUST validate
    /// against the highest-used invoice number BEFORE calling this
    /// (the entity can't know about other aggregates). The handler
    /// also enforces the FR-037 period-lock guard upstream.
    /// </summary>
    public void UpdateNextNumber(int next)
    {
        if (next < 1)
            throw new ArgumentOutOfRangeException(nameof(next),
                "Next invoice number must be ≥ 1.");
        NextNumber = next;
    }

    public void UpdatePaymentTerms(int days)
    {
        if (days is < 0 or > 365)
            throw new ArgumentOutOfRangeException(nameof(days),
                "Payment terms must be 0-365 days.");
        DefaultPaymentTermsDays = days;
    }

    public void UpdateFooterNotes(string? notes) =>
        FooterNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public void UpdateShowQrCode(bool show) => ShowQrCode = show;
    public void UpdateShowLogo(bool show) => ShowLogo = show;
}

public enum InvoiceLanguageMode
{
    /// <summary>Single-language Arabic invoices (default).</summary>
    ArabicOnly = 0,
    /// <summary>Bilingual Arabic + English (SMB+ feature).</summary>
    Bilingual = 1,
}
