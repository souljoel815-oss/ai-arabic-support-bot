namespace EgyptTax.Web.Shared.AppShell;

/// <summary>
/// v5 UI Rebuild — Sprint 1. Defines a module in the new 3-tier
/// "Mission Control" navigation per the Manus 2026-05-15 design
/// spec: 7 module icons in a 64px sidebar; clicking one swaps the
/// 240px sub-nav panel to that module's contextual links. The
/// Dashboard module is single-screen so it has <see cref="HasSubNav"/>
/// = false (no panel renders).
///
/// The icon path is the SVG <c>d=</c> attribute for a 24×24
/// viewBox; render via the existing <c>Icon</c> component.
/// </summary>
public sealed record AppShellModule(
    string Id,
    string IconName,
    string LabelAr,
    string LabelEn,
    string DefaultRoute,
    bool HasSubNav,
    IReadOnlyList<SubNavEntry> SubNavEntries
);

public abstract record SubNavEntry;
public sealed record SubNavSeparator() : SubNavEntry;
public sealed record SubNavGroupHeader(string LabelAr, string LabelEn) : SubNavEntry;
public sealed record SubNavLink(
    string LabelAr,
    string LabelEn,
    string Url,
    string? Search = null
) : SubNavEntry;

/// <summary>
/// Catalog of the 7 modules + their sub-nav contents. Routes
/// match the existing flat URL structure (e.g. <c>/invoices</c>,
/// not <c>/sales/invoices</c>) so we don't break any link in the
/// app — the module grouping is a UI concept, not a URL path.
/// </summary>
public static class AppShellModuleRegistry
{
    public const string DashboardId  = "dashboard";
    public const string SalesId      = "sales";
    public const string PurchasesId  = "purchases";
    public const string InventoryId  = "inventory";
    public const string AccountingId = "accounting";
    public const string ContactsId   = "contacts";
    public const string SettingsId   = "settings";

    public static IReadOnlyList<AppShellModule> All { get; } = new[]
    {
        new AppShellModule(DashboardId, "home", "الرئيسية", "Home", "/", HasSubNav: false,
            SubNavEntries: Array.Empty<SubNavEntry>()),

        new AppShellModule(SalesId, "trending-up", "المبيعات", "Sales", "/invoices", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("عروض الأسعار", "Quotations", "/quotations", "quotation عرض سعر"),
                new SubNavLink("أوامر البيع", "Sales orders", "/sales-orders", "sales order أمر بيع"),
                new SubNavLink("الفواتير", "Invoices", "/invoices", "invoice فاتورة"),
                new SubNavLink("إنشاء فاتورة جماعية", "Bulk invoices", "/invoices/bulk", "bulk جماعي"),
                new SubNavLink("الفواتير المتكررة", "Recurring invoices", "/recurring-invoices", "recurring متكرر"),
                new SubNavLink("تسجيل دفعة عميل", "Customer receipt", "/payments/customer-receipts/new", "receipt دفعة"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("لوحة المبيعات", "Sales dashboard", "/dashboards/sales", "sales dashboard"),
                new SubNavLink("قمع المبيعات", "Sales pipeline", "/reports/sales-pipeline", "pipeline قمع"),
                new SubNavLink("مبيعات المناديب", "Sales by rep", "/reports/sales-by-rep", "rep مندوب"),
                new SubNavLink("عمولات المناديب", "Commissions", "/reports/commissions", "commission عمولة"),
            }),

        new AppShellModule(PurchasesId, "shopping-cart", "المشتريات", "Purchases", "/expenses", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("المصروفات", "Expenses", "/expenses", "expense مصروف"),
                new SubNavLink("تقارير المصروفات", "Expense reports", "/expense-reports", "expense report تقرير مصروف"),
                new SubNavLink("دفعة لمورد", "Pay supplier", "/payments/supplier-payments/new", "supplier payment دفعة مورد"),
                new SubNavLink("مسح إيصال (AI)", "Scan receipt", "/scan-receipt", "scan ocr"),
                new SubNavLink("سجل المسح", "Scan history", "/scan-history", "history scan"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الأصول الثابتة", "Fixed assets"),
                new SubNavLink("الأصول الثابتة", "Fixed assets", "/fixed-assets", "fixed asset أصل"),
                new SubNavLink("أصل جديد", "New asset", "/fixed-assets/new", "new asset"),
                new SubNavSeparator(),
                new SubNavGroupHeader("ضريبة الخصم من المنبع", "Withholding tax"),
                new SubNavLink("الخصم من المنبع", "WHT", "/wht", "wht خصم"),
                new SubNavLink("نموذج 41", "Form 41", "/wht/form41", "form 41 نموذج"),
                new SubNavLink("شهادات الخصم", "WHT certificates", "/certificates", "certificate شهادة"),
            }),

        new AppShellModule(InventoryId, "package", "المخزون", "Inventory", "/items", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("الأصناف", "Items", "/items", "item صنف"),
                new SubNavLink("نقطة البيع (POS)", "Point of sale", "/pos", "pos نقطة بيع"),
                new SubNavLink("جرد المخزون", "Stock count", "/stock-adjustments", "stock count جرد"),
                new SubNavLink("التحويل بين المواقع", "Stock transfer", "/stock-transfer", "transfer تحويل"),
                new SubNavLink("مواقع التخزين", "Stock locations", "/stock-locations", "location موقع"),
                new SubNavLink("الدفعات قاربت الانتهاء", "Expiring lots", "/expiring-lots", "expiring lot صلاحية"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("تقييم المخزون", "Stock valuation", "/reports/stock-valuation", "valuation تقييم"),
                new SubNavLink("جلسات الكاشير", "POS sessions", "/reports/pos-sessions", "pos session جلسة"),
            }),

        new AppShellModule(AccountingId, "calculator", "المحاسبة", "Accounting", "/journals", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("قيود اليومية", "Journal entries", "/journals", "journal قيد"),
                new SubNavLink("شجرة الحسابات", "Chart of accounts", "/settings/chart-of-accounts", "coa حسابات"),
                new SubNavLink("مراكز التكلفة", "Cost centers", "/cost-centers", "cost center مركز تكلفة"),
                new SubNavLink("المشروعات", "Projects", "/projects", "project مشروع"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الكاش والبنوك", "Cash & banks"),
                new SubNavLink("كشوف البنك", "Bank statements", "/payments/bank-statements", "bank statement كشف"),
                new SubNavLink("الدفعات غير المطابقة", "Unmatched payments", "/payments/unmatched", "unmatched غير مطابق"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الضرائب والامتثال", "Tax & compliance"),
                new SubNavLink("لوحة ETA", "ETA dashboard", "/eta-dashboard", "eta لوحة"),
                new SubNavLink("صندوق ETA الوارد", "ETA inbox", "/eta-inbox", "eta inbox"),
                new SubNavLink("إقرار VAT", "VAT return", "/tax/vat-return", "vat return إقرار"),
                new SubNavLink("إقرار ضريبة الدخل", "Income tax return", "/tax/income-tax-return", "income tax دخل"),
                new SubNavLink("تقويم الالتزامات", "Compliance calendar", "/compliance/calendar", "compliance calendar تقويم"),
                new SubNavLink("صحة الامتثال", "Compliance health", "/compliance/health", "compliance health صحة"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الإقفال", "Closing"),
                new SubNavLink("كشكول الإقفال", "Closing cockpit", "/cockpit", "closing cockpit إقفال"),
                new SubNavLink("الموافقات المعلقة", "Approvals", "/approvals", "approval موافقة"),
                new SubNavLink("درع الغرامات", "Penalty shield", "/penalty-shield", "penalty غرامة"),
                new SubNavLink("سجل التدقيق", "Audit log", "/audit-log", "audit سجل"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("دفتر الأستاذ", "General Ledger", "/reports/general-ledger", "general ledger دفتر"),
                new SubNavLink("ميزان المراجعة", "Trial balance", "/reports/trial-balance", "trial balance ميزان"),
                new SubNavLink("التدفقات النقدية", "Cash flow", "/reports/cash-flow", "cash flow تدفقات"),
                new SubNavLink("تقرير VAT الشهري", "VAT monthly", "/reports/vat-monthly", "vat monthly شهري"),
                new SubNavLink("تقرير ضريبة الدوران", "Turnover tax", "/reports/turnover-tax", "turnover دوران"),
            }),

        new AppShellModule(ContactsId, "users", "جهات الاتصال", "Contacts", "/customers", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("العملاء", "Customers", "/customers", "customer عميل"),
                new SubNavLink("الموردين", "Suppliers", "/suppliers", "supplier مورد"),
                new SubNavSeparator(),
                new SubNavGroupHeader("CRM", "CRM"),
                new SubNavLink("الفرص", "Leads", "/leads", "lead فرصة"),
                new SubNavLink("جولات المناديب", "Sales rep routes", "/routes", "route جولة"),
                new SubNavLink("لوحتي الشخصية", "My dashboard", "/dashboards/me", "my dashboard لوحتي"),
            }),

        new AppShellModule(SettingsId, "settings", "الإعدادات", "Settings", "/settings", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("لوحة الإعدادات", "Settings home", "/settings", "settings إعدادات"),
                new SubNavLink("بيانات الشركة", "Company info", "/settings/company", "company شركة"),
                new SubNavLink("السنة المالية", "Fiscal year", "/settings/fiscal-year", "fiscal year سنة"),
                new SubNavLink("الفترات الضريبية", "Tax periods", "/settings/tax-periods", "tax period فترة"),
                new SubNavLink("الأرصدة الافتتاحية", "Opening balances", "/settings/opening-balances", "opening balance افتتاحي"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التهيئة", "Configuration"),
                new SubNavLink("VAT", "VAT categories", "/settings/vat-categories", "vat فئات"),
                new SubNavLink("WHT", "WHT categories", "/settings/wht-categories", "wht فئات"),
                new SubNavLink("فئات المصروفات", "Expense categories", "/settings/expense-categories", "expense category فئة"),
                new SubNavLink("طرق الدفع", "Payment methods", "/settings/payment-methods", "payment method طريقة"),
                new SubNavLink("الحسابات النقدية", "Cash accounts", "/settings/cash-accounts", "cash account نقدي"),
                new SubNavLink("قوالب عروض الأسعار", "Quotation templates", "/settings/quotation-templates", "quotation template قالب"),
                new SubNavLink("Webhooks", "Webhooks", "/settings/webhooks", "webhook"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الاستيراد والإحالات", "Import & referrals"),
                new SubNavLink("استيراد البيانات", "Data import", "/data-import", "import استيراد"),
                new SubNavLink("الإحالات", "Referrals", "/settings/referrals", "referral إحالة"),
            }),
    };

    public static AppShellModule? Get(string id) =>
        All.FirstOrDefault(m => m.Id == id);

    /// <summary>
    /// Maps a URL path to its owning module. Order matters — more
    /// specific prefixes win (e.g. <c>/settings/chart-of-accounts</c>
    /// belongs to Accounting, not Settings, even though it sits
    /// under <c>/settings/</c>). Falls back to Dashboard for the
    /// root path or anything unmatched (e.g. an admin-only page
    /// the operator opened from a deep link).
    /// </summary>
    public static string ResolveActive(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return DashboardId;

        // Special-case routes that visually belong to a different
        // module than their URL prefix would suggest:
        if (path.StartsWith("/settings/chart-of-accounts", StringComparison.OrdinalIgnoreCase))
            return AccountingId;

        // Prefix match against each module's sub-nav links.
        foreach (var module in All)
        {
            foreach (var entry in module.SubNavEntries)
            {
                if (entry is not SubNavLink link) continue;
                if (path.Equals(link.Url, StringComparison.OrdinalIgnoreCase)) return module.Id;
                if (path.StartsWith(link.Url + "/", StringComparison.OrdinalIgnoreCase)) return module.Id;
            }
        }

        // Untracked routes fall back to Dashboard so the user can
        // always click a module to navigate elsewhere.
        return DashboardId;
    }
}
