namespace EgyptTax.Web.Licensing;

/// <summary>
/// Gux.13 — feature-flag identifiers used by
/// <c>LicenseGate.Require(Feature)</c>. The license payload's
/// <c>Features[]</c> array holds these as strings; the gate checks
/// membership before allowing a gated handler to run.
///
/// Constants only — using a class of strings rather than an enum
/// because the wire format is the string and we need stable values
/// across versions. Reordering an enum would silently break tokens.
///
/// Each constant maps to a row in the §7.2 feature matrix in
/// <c>gux-13-admin-panel.md</c>.
/// </summary>
public static class Feature
{
    // --- Operations (SMB+) ---
    public const string BankImport      = "bank_import";
    public const string BulkInvoice     = "bulk_invoice";
    public const string MultiCashbox    = "multi_cashbox";
    public const string ClosingCockpit  = "closing_cockpit";
    public const string TrialBalance    = "trial_balance";
    public const string CustomChartOfAccounts = "chart_of_accounts_custom";

    // --- Advanced (SMB+) ---
    public const string MultiUser       = "multi_user";
    public const string SmtpDirectEmail = "smtp_email";
    public const string AutoBackup      = "auto_backup";
    public const string AuditLog        = "audit_log";
    public const string InvoiceTemplates = "invoice_templates";
    public const string BilingualInvoices = "bilingual_invoices";

    // --- Enterprise+ ---
    public const string MultiCompany    = "multi_company";
    public const string IncomeTaxReturn = "income_tax_return";
    public const string WhtCertificateMgmt = "wht_certificate_mgmt";
    public const string ComplianceHealth = "compliance_health";
    public const string PrioritySupport = "priority_support";
    public const string CloudBackup     = "cloud_backup";
    public const string ArabicAiAssistant = "ai_assistant";

    // --- Firm only ---
    public const string FirmPortal      = "firm_portal";
    public const string CompanySwitcher = "company_switcher";
    public const string CommissionLedger = "commission_ledger";
    public const string ClientOnboardingWizard = "client_onboarding";
    public const string BulkCrossCompany = "bulk_cross_company";

    // --- G2 / G3 differentiators (SMB+) ---
    public const string EtaItemCodeSuggester = "eta_item_code_suggester";
    public const string WhatsAppDelivery = "whatsapp_delivery";
    public const string ReceiptOcr      = "receipt_ocr";

    /// <summary>
    /// Default feature sets per edition. Used by `Issue-License.ps1`
    /// to populate the <c>Features[]</c> array when minting a token,
    /// and as the in-app fallback when a payload omits the array
    /// (pre-Gux.13 tokens).
    /// </summary>
    public static IReadOnlyList<string> DefaultsFor(LicenseEdition edition) => edition switch
    {
        LicenseEdition.Solo => Array.Empty<string>(),
        // Solo gets only the core compliance features (which are
        // not gated at all — they ship to every edition). Everything
        // operations-grade requires SMB upgrade.

        LicenseEdition.Smb => new[]
        {
            BankImport, BulkInvoice, MultiCashbox, ClosingCockpit,
            TrialBalance, CustomChartOfAccounts, MultiUser,
            SmtpDirectEmail, AutoBackup, AuditLog, InvoiceTemplates,
            BilingualInvoices, EtaItemCodeSuggester, WhatsAppDelivery,
            ReceiptOcr,
        },
        LicenseEdition.Enterprise => new[]
        {
            // SMB features ...
            BankImport, BulkInvoice, MultiCashbox, ClosingCockpit,
            TrialBalance, CustomChartOfAccounts, MultiUser,
            SmtpDirectEmail, AutoBackup, AuditLog, InvoiceTemplates,
            BilingualInvoices, EtaItemCodeSuggester, WhatsAppDelivery,
            ReceiptOcr,
            // ... + Enterprise additions
            MultiCompany, IncomeTaxReturn, WhtCertificateMgmt,
            ComplianceHealth, PrioritySupport, CloudBackup,
            ArabicAiAssistant,
        },
        LicenseEdition.Firm => new[]
        {
            // Enterprise features ...
            BankImport, BulkInvoice, MultiCashbox, ClosingCockpit,
            TrialBalance, CustomChartOfAccounts, MultiUser,
            SmtpDirectEmail, AutoBackup, AuditLog, InvoiceTemplates,
            BilingualInvoices, EtaItemCodeSuggester, WhatsAppDelivery,
            ReceiptOcr, MultiCompany, IncomeTaxReturn,
            WhtCertificateMgmt, ComplianceHealth, PrioritySupport,
            CloudBackup, ArabicAiAssistant,
            // ... + Firm-only additions
            FirmPortal, CompanySwitcher, CommissionLedger,
            ClientOnboardingWizard, BulkCrossCompany,
        },
        LicenseEdition.Trial => new[]
        {
            // Trial unlocks all Enterprise features (NOT Firm Portal)
            // so the prospect can experience the full Enterprise tier.
            BankImport, BulkInvoice, MultiCashbox, ClosingCockpit,
            TrialBalance, CustomChartOfAccounts, MultiUser,
            SmtpDirectEmail, AutoBackup, AuditLog, InvoiceTemplates,
            BilingualInvoices, EtaItemCodeSuggester, WhatsAppDelivery,
            ReceiptOcr, MultiCompany, IncomeTaxReturn,
            WhtCertificateMgmt, ComplianceHealth, PrioritySupport,
            CloudBackup, ArabicAiAssistant,
        },
        _ => Array.Empty<string>(),
    };

    /// <summary>The minimum edition that includes a given feature.
    /// Used by the upgrade-prompt UI to show "this is a SMB feature"
    /// or "this is an Enterprise feature" when a Solo customer hits
    /// a gate.</summary>
    public static LicenseEdition MinimumEditionFor(string feature) => feature switch
    {
        FirmPortal             or
        CompanySwitcher        or
        CommissionLedger       or
        ClientOnboardingWizard or
        BulkCrossCompany       => LicenseEdition.Firm,

        MultiCompany         or
        IncomeTaxReturn      or
        WhtCertificateMgmt   or
        ComplianceHealth     or
        PrioritySupport      or
        CloudBackup          or
        ArabicAiAssistant    => LicenseEdition.Enterprise,

        _                    => LicenseEdition.Smb,
    };
}
