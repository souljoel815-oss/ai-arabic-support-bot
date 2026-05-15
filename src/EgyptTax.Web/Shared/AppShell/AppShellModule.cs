namespace EgyptTax.Web.Shared.AppShell;

/// <summary>
/// v5 UI Rebuild — Sprint 1 (updated 2026-05-15).
/// Defines a module in the 3-tier "Mission Control" navigation:
///   Layer 1: 200px sidebar with icons + Arabic labels always visible
///   Layer 2: 220px sub-nav panel with contextual links
///   Layer 3: Main content area
///
/// The Dashboard module is single-screen (HasSubNav = false).
/// Icon names map to the existing <c>&lt;Icon Name="..." /&gt;</c> component.
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
    string? Search = null,
    string IconName = "file-text"
) : SubNavEntry;

/// <summary>
/// Quick action for the Command Palette (Ctrl+K).
/// </summary>
public sealed record QuickAction(
    string LabelAr,
    string LabelEn,
    string Url,
    string IconName
);

/// <summary>
/// Catalog of the 7 modules + their sub-nav contents + quick actions.
/// Routes match the existing flat URL structure (e.g. <c>/invoices</c>,
/// not <c>/sales/invoices</c>) so we don't break any link in the app.
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
                new SubNavLink("عروض الأسعار", "Quotations", "/quotations", "quotation عرض سعر", "file-text"),
                new SubNavLink("أوامر البيع", "Sales orders", "/sales-orders", "sales order أمر بيع", "shopping-cart"),
                new SubNavLink("الفواتير", "Invoices", "/invoices", "invoice فاتورة", "receipt"),
                new SubNavLink("إنشاء فاتورة جماعية", "Bulk invoices", "/invoices/bulk", "bulk جماعي", "boxes"),
                new SubNavLink("الفواتير المتكررة", "Recurring invoices", "/recurring-invoices", "recurring متكرر", "calendar-range"),
                new SubNavLink("تسجيل دفعة عميل", "Customer receipt", "/payments/customer-receipts/new", "receipt دفعة", "wallet"),
                new SubNavLink("الدفعات المقدمة", "Customer advances", "/customer-advances", "customer advance دفعة مقدمة", "wallet"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("لوحة المبيعات", "Sales dashboard", "/dashboards/sales", "sales dashboard", "bar-chart"),
                new SubNavLink("قمع المبيعات", "Sales pipeline", "/reports/sales-pipeline", "pipeline قمع", "target"),
                new SubNavLink("مبيعات المناديب", "Sales by rep", "/reports/sales-by-rep", "rep مندوب", "users"),
                new SubNavLink("عمولات المناديب", "Commissions", "/reports/commissions", "commission عمولة", "percent"),
            }),

        new AppShellModule(PurchasesId, "shopping-cart", "المشتريات", "Purchases", "/purchase-invoices", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("فواتير الموردين", "Purchase invoices", "/purchase-invoices", "purchase invoice فاتورة شراء", "receipt"),
                new SubNavLink("المصروفات", "Expenses", "/expenses", "expense مصروف", "credit-card"),
                new SubNavLink("تقارير المصروفات", "Expense reports", "/expense-reports", "expense report تقرير مصروف", "file-text"),
                new SubNavLink("دفعة لمورد", "Pay supplier", "/payments/supplier-payments/new", "supplier payment دفعة مورد", "wallet"),
                new SubNavLink("مسح إيصال (AI)", "Scan receipt", "/scan-receipt", "scan ocr", "zap"),
                new SubNavLink("سجل المسح", "Scan history", "/scan-history", "history scan", "archive"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الأصول الثابتة", "Fixed assets"),
                new SubNavLink("الأصول الثابتة", "Fixed assets", "/fixed-assets", "fixed asset أصل", "building"),
                new SubNavLink("أصل جديد", "New asset", "/fixed-assets/new", "new asset", "plus"),
                new SubNavSeparator(),
                new SubNavGroupHeader("ضريبة الخصم من المنبع", "Withholding tax"),
                new SubNavLink("الخصم من المنبع", "WHT", "/wht", "wht خصم", "scissors"),
                new SubNavLink("نموذج 41", "Form 41", "/wht/form41", "form 41 نموذج", "file-text"),
                new SubNavLink("شهادات الخصم", "WHT certificates", "/certificates", "certificate شهادة", "shield-check"),
            }),

        new AppShellModule(InventoryId, "package", "المخزون", "Inventory", "/items", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("الأصناف", "Items", "/items", "item صنف", "package"),
                new SubNavLink("نقطة البيع (POS)", "Point of sale", "/pos", "pos نقطة بيع", "credit-card"),
                new SubNavLink("جرد المخزون", "Stock count", "/stock-adjustments", "stock count جرد", "check-square"),
                new SubNavLink("التحويل بين المواقع", "Stock transfer", "/stock-transfer", "transfer تحويل", "arrow-right"),
                new SubNavLink("مواقع التخزين", "Stock locations", "/stock-locations", "location موقع", "boxes"),
                new SubNavLink("الدفعات قاربت الانتهاء", "Expiring lots", "/expiring-lots", "expiring lot صلاحية", "alert-triangle"),
                new SubNavSeparator(),
                new SubNavGroupHeader("إعادة الطلب", "Reordering"),
                new SubNavLink("قواعد إعادة الطلب", "Reorder rules", "/reorder-rules", "reorder rule قاعدة", "edit"),
                new SubNavLink("اقتراحات إعادة الطلب", "Reorder suggestions", "/reorder-suggestions", "reorder suggestion اقتراح", "alert-triangle"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("تقييم المخزون", "Stock valuation", "/reports/stock-valuation", "valuation تقييم", "bar-chart"),
                new SubNavLink("جلسات الكاشير", "POS sessions", "/reports/pos-sessions", "pos session جلسة", "clock"),
            }),

        new AppShellModule(AccountingId, "calculator", "المحاسبة", "Accounting", "/journals", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("قيود اليومية", "Journal entries", "/journals", "journal قيد", "edit"),
                new SubNavLink("قوالب القيود الدورية", "Recurring JV templates", "/journal-templates", "journal template قالب recurring", "file-text"),
                new SubNavLink("المصروفات المدفوعة مقدماً", "Prepaid expenses", "/prepaid-expenses", "prepaid expense مدفوع مقدم", "calendar"),
                new SubNavLink("شجرة الحسابات", "Chart of accounts", "/settings/chart-of-accounts", "coa حسابات", "boxes"),
                new SubNavLink("المطابقة البنكية", "Bank reconciliation", "/payments/bank-statements", "bank statement كشف", "landmark"),
                new SubNavLink("تحويل أموال", "Fund transfer", "/cash-transfer", "fund transfer تحويل", "arrow-right"),
                new SubNavLink("مسحوبات المالك", "Owner drawings", "/owner-drawings", "owner drawings مسحوبات", "wallet"),
                new SubNavLink("الدفعات غير المخصصة", "Unmatched payments", "/payments/unmatched", "unmatched payment دفعة", "alert-triangle"),
                new SubNavLink("مراكز التكلفة", "Cost centers", "/cost-centers", "cost center مركز تكلفة", "tag"),
                new SubNavLink("سجل التدقيق", "Audit log", "/audit-log", "audit log سجل تدقيق", "shield-check"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الضرائب", "Taxes"),
                new SubNavLink("ضريبة القيمة المضافة", "VAT return", "/tax/vat-return", "vat return إقرار", "percent"),
                new SubNavLink("خصم من المنبع", "WHT", "/wht", "wht خصم", "scissors"),
                new SubNavLink("استيراد WHT الوارد", "Inbound WHT import", "/wht/inbound", "wht inbound استيراد", "archive"),
                new SubNavLink("ضريبة الدخل", "Income tax", "/tax/income-tax-return", "income tax دخل", "landmark"),
                new SubNavLink("تقويم الالتزامات", "Compliance calendar", "/compliance/calendar", "compliance calendar تقويم", "calendar"),
                new SubNavLink("صحة الالتزام", "Compliance health", "/compliance/health", "compliance health صحة", "shield"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الفاتورة الإلكترونية (ETA)", "E-invoicing (ETA)"),
                new SubNavLink("لوحة ETA", "ETA dashboard", "/eta-dashboard", "eta dashboard لوحة", "activity"),
                new SubNavLink("صندوق ETA الوارد", "ETA inbox", "/eta-inbox", "eta inbox صندوق", "archive"),
                new SubNavLink("حزمة فحص ضريبي", "Inspection bundle", "/inspection-bundle", "inspection bundle فحص", "shield-check"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الإقفال", "Closing"),
                new SubNavLink("قيود التسوية", "Adjusting entries", "/journals", "adjusting entries تسوية", "scale"),
                new SubNavLink("كشكول الإقفال", "Closing cockpit", "/cockpit", "closing cockpit إقفال", "calendar-range"),
                new SubNavLink("قفل الفترة", "Period lock", "/approvals", "period lock قفل", "lock"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("ميزان المراجعة", "Trial balance", "/reports/trial-balance", "trial balance ميزان", "scale"),
                new SubNavLink("دفتر الأستاذ", "General Ledger", "/reports/general-ledger", "general ledger دفتر", "book"),
                new SubNavLink("قائمة الدخل", "Profit & loss", "/reports/profit-loss", "p&l دخل", "trending-up"),
                new SubNavLink("الميزانية", "Balance sheet", "/reports/balance-sheet", "balance sheet ميزانية", "scale"),
                new SubNavLink("التدفقات النقدية", "Cash flow", "/reports/cash-flow", "cash flow تدفقات", "trending-up"),
                new SubNavLink("تقرير مراكز التكلفة", "Cost-centers report", "/reports/cost-centers", "cost center report تقرير", "tag"),
                new SubNavLink("VAT الشهري", "Monthly VAT report", "/reports/vat-monthly", "vat monthly شهري", "percent"),
                new SubNavLink("ضريبة رقم الأعمال", "Turnover tax report", "/reports/turnover-tax", "turnover tax أعمال", "percent"),
                new SubNavLink("الدخل الخاضع للضريبة", "Taxable income report", "/reports/taxable-income", "taxable income دخل", "landmark"),
                new SubNavLink("درع الغرامات", "Penalty shield", "/penalty-shield", "penalty غرامة", "shield-check"),
            }),

        new AppShellModule(ContactsId, "users", "جهات الاتصال", "Contacts", "/customers", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("العملاء", "Customers", "/customers", "customer عميل", "user"),
                new SubNavLink("الموردين", "Suppliers", "/suppliers", "supplier مورد", "building"),
                new SubNavSeparator(),
                new SubNavGroupHeader("CRM", "CRM"),
                new SubNavLink("الفرص", "Leads", "/leads", "lead فرصة", "target"),
                new SubNavLink("المشاريع", "Projects", "/projects", "project مشروع", "check-square"),
                new SubNavLink("جولات المناديب", "Sales rep routes", "/routes", "route جولة", "calendar"),
                new SubNavLink("لوحتي الشخصية", "My dashboard", "/dashboards/me", "my dashboard لوحتي", "activity"),
            }),

        new AppShellModule(SettingsId, "settings", "الإعدادات", "Settings", "/settings", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("لوحة الإعدادات", "Settings home", "/settings", "settings إعدادات", "settings"),
                new SubNavLink("بيانات الشركة", "Company info", "/settings/company", "company شركة", "building"),
                new SubNavLink("السنة المالية", "Fiscal year", "/settings/fiscal-year", "fiscal year سنة", "calendar"),
                new SubNavLink("الفترات الضريبية", "Tax periods", "/settings/tax-periods", "tax period فترة", "calendar-range"),
                new SubNavLink("الأرصدة الافتتاحية", "Opening balances", "/settings/opening-balances", "opening balance افتتاحي", "scale"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التهيئة", "Configuration"),
                new SubNavLink("VAT", "VAT categories", "/settings/vat-categories", "vat فئات", "percent"),
                new SubNavLink("WHT", "WHT categories", "/settings/wht-categories", "wht فئات", "scissors"),
                new SubNavLink("شروط الدفع", "Payment terms", "/settings/payment-terms", "payment term شرط دفع", "calendar"),
                new SubNavLink("فئات المصروفات", "Expense categories", "/settings/expense-categories", "expense category فئة", "tag"),
                new SubNavLink("طرق الدفع", "Payment methods", "/settings/payment-methods", "payment method طريقة", "credit-card"),
                new SubNavLink("الحسابات النقدية", "Cash accounts", "/settings/cash-accounts", "cash account نقدي", "wallet"),
                new SubNavLink("العملات", "Currencies", "/currencies", "currency عملة", "globe"),
                new SubNavLink("قوالب عروض الأسعار", "Quotation templates", "/settings/quotation-templates", "quotation template قالب", "file-text"),
                new SubNavLink("قوائم الأسعار", "Pricelists", "/settings/pricelists", "pricelist قائمة سعر", "tag"),
                new SubNavLink("فرق المبيعات", "Sales teams", "/settings/sales-teams", "sales team فريق مبيعات", "users"),
                new SubNavLink("Webhooks", "Webhooks", "/settings/webhooks", "webhook", "zap"),
                new SubNavSeparator(),
                new SubNavGroupHeader("الاستيراد والإحالات", "Import & referrals"),
                new SubNavLink("استيراد البيانات", "Data import", "/data-import", "import استيراد", "archive"),
                new SubNavLink("الإحالات", "Referrals", "/settings/referrals", "referral إحالة", "users"),
            }),
    };

    /// <summary>Quick actions shown in the Command Palette (Ctrl+K).</summary>
    public static IReadOnlyList<QuickAction> QuickActions { get; } = new[]
    {
        new QuickAction("فاتورة جديدة", "New invoice", "/invoices/new", "receipt"),
        new QuickAction("عرض سعر جديد", "New quotation", "/quotations/new", "file-text"),
        new QuickAction("مصروف جديد", "New expense", "/expenses/new", "credit-card"),
        new QuickAction("قيد يومية جديد", "New journal entry", "/journals/new", "edit"),
        new QuickAction("تسجيل دفعة", "Record payment", "/payments/customer-receipts/new", "wallet"),
    };

    public static AppShellModule? Get(string id) =>
        All.FirstOrDefault(m => m.Id == id);

    /// <summary>
    /// Maps a URL path to its owning module. Order matters — more
    /// specific prefixes win (e.g. <c>/settings/chart-of-accounts</c>
    /// belongs to Accounting, not Settings). Falls back to Dashboard
    /// for the root path or anything unmatched.
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

        return DashboardId;
    }

    /// <summary>
    /// Returns all sub-nav links across all modules — used by the
    /// Command Palette to provide searchable screen navigation.
    /// </summary>
    public static IEnumerable<(string Label, string Url, string ModuleLabel, string IconName)> GetAllSearchableItems(bool arabic = true)
    {
        foreach (var module in All)
        {
            foreach (var entry in module.SubNavEntries)
            {
                if (entry is SubNavLink link)
                {
                    var label = arabic ? link.LabelAr : link.LabelEn;
                    var modLabel = arabic ? module.LabelAr : module.LabelEn;
                    yield return (label, link.Url, modLabel, link.IconName);
                }
            }
        }
    }
}
