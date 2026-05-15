using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B1 — the single-tenant company record. The MVP installation has
/// exactly one row; FR-005 mandates the fields required for legal
/// invoice rendering (issuer name, TIN, commercial registration,
/// address) plus operational config (fiscal-year start month,
/// default currency / language). The same row is also the issuer
/// block on every ETA eInvoice submission per the
/// <c>eta-einvoice.schema.json</c> contract.
/// </summary>
public sealed class Company
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ArabicEnglishText LegalName { get; private set; }
    public string TaxRegistrationNumber { get; private set; } = default!;
    public string CommercialRegistrationNumber { get; private set; } = default!;
    public PostalAddress Address { get; private set; }
    public string? LogoPath { get; private set; }
    public int FiscalYearStartMonth { get; private set; } = 1;
    public string DefaultCurrency { get; private set; } = "EGP";
    public Language DefaultLanguage { get; private set; } = Language.Ar;
    public string TaxpayerActivityCode { get; private set; } = default!;

    /// <summary>
    /// Day 8 / Law 6 of 2025 — tax regime the company operates under.
    /// <see cref="TaxRegime.Standard"/> = monthly VAT + full corporate
    /// income tax; <see cref="TaxRegime.Law6Simplified"/> = quarterly
    /// VAT + bracketed turnover tax (only available to companies under
    /// the EGP 15M annual turnover threshold). Defaults to Standard
    /// for backward-compat with installs that pre-date the new regime.
    /// </summary>
    public TaxRegime TaxRegime { get; private set; } = TaxRegime.Standard;

    /// <summary>v4 C.9 — visual template the invoice PDF renderer
    /// applies. Three hard-coded variants ship in v4 (Classic /
    /// Modern / Minimal); a full template designer is intentionally
    /// out-of-scope until a customer asks. Defaults to Classic so
    /// pre-v4 installs render unchanged.</summary>
    public InvoicePdfTemplate DefaultPdfTemplate { get; private set; } = InvoicePdfTemplate.Classic;

    /// <summary>v5 A.6 — when true, /pos refuses to record sales
    /// without an open <c>PosSession</c>; first click of the day
    /// shows the "Enter opening cash" modal. Default false so
    /// pre-v5 installs continue to work direct-to-sale (the v4
    /// behaviour). Operator opts in via /settings/company.</summary>
    public bool RequirePosSession { get; private set; }

    private Company() { }

    public Company(
        ArabicEnglishText legalName,
        EgyptianTin taxRegistrationNumber,
        string commercialRegistrationNumber,
        PostalAddress address,
        string taxpayerActivityCode,
        int fiscalYearStartMonth = 1,
        Language defaultLanguage = Language.Ar
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commercialRegistrationNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxpayerActivityCode);
        if (fiscalYearStartMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fiscalYearStartMonth),
                "Fiscal-year start month must be between 1 and 12."
            );
        }

        LegalName = legalName;
        TaxRegistrationNumber = taxRegistrationNumber.Value;
        CommercialRegistrationNumber = commercialRegistrationNumber;
        Address = address;
        TaxpayerActivityCode = taxpayerActivityCode;
        FiscalYearStartMonth = fiscalYearStartMonth;
        DefaultLanguage = defaultLanguage;
    }

    public void UpdateAddress(PostalAddress address) => Address = address;

    public void UpdateLogo(string? logoPath) => LogoPath = logoPath;

    public void UpdateTaxpayerActivityCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        TaxpayerActivityCode = code;
    }

    /// <summary>Day 8 — switch the company between Standard and the Law 6 simplified regime.</summary>
    public void UpdateTaxRegime(TaxRegime regime) => TaxRegime = regime;

    /// <summary>v4 C.9 — switch the default invoice PDF template.</summary>
    public void UpdateDefaultPdfTemplate(InvoicePdfTemplate template) =>
        DefaultPdfTemplate = template;

    /// <summary>v5 A.6 — toggle the POS-sessions requirement.</summary>
    public void UpdateRequirePosSession(bool require) => RequirePosSession = require;
}

/// <summary>v4 C.9 — visual variants of the sales-invoice PDF.
/// Three hard-coded styles ship in v4; a designer mode lands when
/// a customer asks for it.</summary>
public enum InvoicePdfTemplate
{
    /// <summary>Pre-v4 look. Black-on-white, plain headers, simple
    /// bordered tables — the most-conservative option.</summary>
    Classic,
    /// <summary>Slate-blue accent band on the header + colored
    /// totals row. Used by SMBs that want to look professional
    /// without going corporate.</summary>
    Modern,
    /// <summary>Generous whitespace, gray accent rules, minimal
    /// bordering. Reads as "boutique consulting firm" rather than
    /// "tax invoice."</summary>
    Minimal,
}

/// <summary>
/// Day 8 / Law 6 of 2025 — tax regime classification. Determines the
/// VAT-return cadence + the income-tax compute path.
/// </summary>
public enum TaxRegime
{
    /// <summary>Monthly VAT, full corporate income tax (default for businesses &gt;15M EGP).</summary>
    Standard,
    /// <summary>Quarterly VAT, bracketed turnover tax (Law 6 of 2025; eligibility cap = EGP 15M annual revenue).</summary>
    Law6Simplified,
}
