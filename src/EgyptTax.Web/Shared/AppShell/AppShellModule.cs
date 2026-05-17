using EgyptTax.Web.Licensing;

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
///
/// Edition gating (v5.1 — Gux.13 enforcement): top-level modules and
/// individual sub-nav links carry an optional <c>RequiredFeature</c>
/// (a <see cref="Feature"/> constant). When non-null, the sidebar
/// renderer asks <see cref="EditionGate.Allows"/> and hides the entry
/// when the current install's edition doesn't include the feature.
/// Solo customers therefore see a much shorter sidebar than Firm
/// operators — and a URL-direct attempt at a hidden page is caught
/// by the page-level <see cref="EditionGate.Require"/> call which
/// throws <see cref="LicenseRestrictionException"/> and lands on the
/// friendly upgrade page.
/// </summary>
public sealed record AppShellModule(
    string Id,
    string IconName,
    string LabelAr,
    string LabelEn,
    string DefaultRoute,
    bool HasSubNav,
    IReadOnlyList<SubNavEntry> SubNavEntries,
    string? RequiredFeature = null
);

public abstract record SubNavEntry;
public sealed record SubNavSeparator() : SubNavEntry;
public sealed record SubNavGroupHeader(string LabelAr, string LabelEn) : SubNavEntry;
public sealed record SubNavLink(
    string LabelAr,
    string LabelEn,
    string Url,
    string? Search = null,
    string IconName = "file-text",
    IReadOnlyList<string>? AliasUrls = null,
    string? RequiredFeature = null
) : SubNavEntry;

/// <summary>
/// Quick action for the Command Palette (Ctrl+K). Gets the same
/// <c>RequiredFeature</c> treatment as <see cref="SubNavLink"/> so a
/// Solo customer's palette doesn't suggest actions they can't run.
/// </summary>
public sealed record QuickAction(
    string LabelAr,
    string LabelEn,
    string Url,
    string IconName,
    string? RequiredFeature = null
);

/// <summary>
/// Edition-aware visibility helpers. Pure read against
/// <see cref="EditionGate"/>; no caching needed because the gate is
/// already volatile-cheap and an in-session license upgrade should
/// immediately surface new entries.
/// </summary>
public static class AppShellVisibility
{
    public static bool IsAllowed(this SubNavLink link) =>
        link.RequiredFeature is null || EditionGate.Allows(link.RequiredFeature);

    public static bool IsAllowed(this AppShellModule module) =>
        module.RequiredFeature is null || EditionGate.Allows(module.RequiredFeature);

    public static bool IsAllowed(this QuickAction action) =>
        action.RequiredFeature is null || EditionGate.Allows(action.RequiredFeature);

    /// <summary>
    /// Filters a sub-nav list, also dropping group-headers + separators
    /// that become orphaned when every link under them is hidden. A
    /// header followed immediately by another header / separator / EOL
    /// after filtering is removed; a separator at the start / end / next
    /// to another separator is removed.
    /// </summary>
    public static IReadOnlyList<SubNavEntry> FilterForCurrentEdition(this IReadOnlyList<SubNavEntry> entries)
    {
        // Pass 1: drop hidden links.
        var kept = new List<SubNavEntry>(entries.Count);
        foreach (var e in entries)
        {
            if (e is SubNavLink link && !link.IsAllowed()) continue;
            kept.Add(e);
        }
        // Pass 2: drop orphaned headers (header followed by no link before
        // the next header/separator/EOL) and collapsing/edge separators.
        var cleaned = new List<SubNavEntry>(kept.Count);
        for (int i = 0; i < kept.Count; i++)
        {
            var e = kept[i];
            if (e is SubNavGroupHeader)
            {
                bool hasLinkBeforeNextHeaderOrEnd = false;
                for (int j = i + 1; j < kept.Count; j++)
                {
                    if (kept[j] is SubNavGroupHeader or SubNavSeparator) break;
                    if (kept[j] is SubNavLink) { hasLinkBeforeNextHeaderOrEnd = true; break; }
                }
                if (!hasLinkBeforeNextHeaderOrEnd) continue;
            }
            if (e is SubNavSeparator)
            {
                if (cleaned.Count == 0) continue;                       // leading
                if (cleaned[^1] is SubNavSeparator) continue;           // double
                // Trailing separator handled by trim below.
            }
            cleaned.Add(e);
        }
        while (cleaned.Count > 0 && cleaned[^1] is SubNavSeparator)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }
        return cleaned;
    }
}

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
                new SubNavLink("إنشاء فاتورة جماعية", "Bulk invoices", "/invoices/bulk", "bulk جماعي", "boxes",
                    RequiredFeature: Feature.BulkInvoice),
                new SubNavLink("الفواتير المتكررة", "Recurring invoices", "/recurring-invoices", "recurring متكرر", "calendar-range"),
                new SubNavLink("تسجيل دفعة عميل", "Customer receipt", "/payments/customer-receipts/new", "receipt دفعة", "wallet"),
                new SubNavLink("الدفعات المقدمة", "Customer advances", "/customer-advances", "customer advance دفعة مقدمة", "wallet"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("تقارير المبيعات", "Sales reports", "/dashboards/sales",
                    "sales dashboard pipeline rep commission لوحة قمع مندوب عمولة", "bar-chart",
                    AliasUrls: new[]
                    {
                        "/reports/sales-pipeline", "/reports/sales-by-rep", "/reports/commissions",
                    }),
            }),

        new AppShellModule(PurchasesId, "shopping-cart", "المشتريات", "Purchases", "/purchase-invoices", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("فواتير الموردين", "Purchase invoices", "/purchase-invoices", "purchase invoice فاتورة شراء", "receipt"),
                new SubNavLink("المصروفات", "Expenses", "/expenses", "expense مصروف", "credit-card"),
                new SubNavLink("تقارير المصروفات", "Expense reports", "/expense-reports", "expense report تقرير مصروف", "file-text"),
                new SubNavLink("دفعة لمورد", "Pay supplier", "/payments/supplier-payments/new", "supplier payment دفعة مورد", "wallet"),
                new SubNavLink("مسح إيصال (AI)", "Scan receipt", "/scan-receipt", "scan ocr", "zap",
                    RequiredFeature: Feature.ReceiptOcr),
                new SubNavLink("سجل المسح", "Scan history", "/scan-history", "history scan", "archive",
                    RequiredFeature: Feature.ReceiptOcr),
                new SubNavLink("الأصول الثابتة", "Fixed assets", "/fixed-assets", "fixed asset أصل", "building",
                    AliasUrls: new[] { "/fixed-assets/new" }),
                new SubNavSeparator(),
                new SubNavGroupHeader("ضريبة الخصم من المنبع", "Withholding tax"),
                new SubNavLink("الخصم من المنبع", "WHT", "/wht",
                    "wht form 41 inbound certificates نموذج شهادات استيراد خصم", "scissors",
                    AliasUrls: new[]
                    {
                        "/wht/form41", "/wht/inbound", "/certificates",
                    }),
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
                new SubNavLink("تكلفة استيراد جديدة", "New landed cost", "/landed-costs/new", "landed cost import شحن جمارك", "package"),
                new SubNavSeparator(),
                new SubNavLink("إعادة الطلب", "Reordering", "/reorder-rules",
                    "reorder rules suggestions قواعد اقتراحات", "edit",
                    AliasUrls: new[] { "/reorder-suggestions" }),
                new SubNavLink("تقارير المخزون", "Inventory reports", "/reports/stock-valuation",
                    "stock valuation pos sessions تقييم جلسات الكاشير", "bar-chart",
                    AliasUrls: new[] { "/reports/pos-sessions" }),
            }),

        new AppShellModule(AccountingId, "calculator", "المحاسبة", "Accounting", "/journals", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                // v5 sidebar consolidation — multi-link groups collapse to a
                // single entry that lands on the canonical page; the
                // SettingsTabStrip on each page surfaces siblings as tabs.
                // AliasUrls keep the entry highlighted while navigating.
                new SubNavLink("قيود اليومية", "Journal entries", "/journals", "journal قيد", "edit"),
                new SubNavLink("قوالب القيود الدورية", "Recurring JV templates", "/journal-templates", "journal template قالب recurring", "file-text"),
                new SubNavLink("التراكم والاعتراف", "Accruals & recognition", "/prepaid-expenses",
                    "prepaid deferred unearned مدفوع مقدم مؤجل", "calendar",
                    AliasUrls: new[] { "/deferred-revenue" }),
                new SubNavLink("شجرة الحسابات", "Chart of accounts", "/settings/chart-of-accounts", "coa حسابات", "boxes"),
                new SubNavLink("المطابقة البنكية", "Bank reconciliation", "/payments/bank-statements", "bank statement كشف", "landmark",
                    RequiredFeature: Feature.BankImport),
                new SubNavLink("تحويل أموال", "Fund transfer", "/cash-transfer", "fund transfer تحويل", "arrow-right"),
                new SubNavLink("مسحوبات المالك", "Owner drawings", "/owner-drawings", "owner drawings مسحوبات", "wallet"),
                new SubNavLink("تسوية فروق العملة", "FX adjustment", "/accounting/fx-adjustment", "fx fx-adjustment فروق عملة realized", "scale"),
                new SubNavLink("تقييم العملة الأجنبية", "FX revaluation", "/reports/fx-revaluation", "fx revaluation unrealized تقييم غير محقق", "scale"),
                new SubNavLink("الدفعات غير المخصصة", "Unmatched payments", "/payments/unmatched", "unmatched payment دفعة", "alert-triangle"),
                new SubNavLink("مراكز التكلفة", "Cost centers", "/cost-centers", "cost center مركز تكلفة", "tag"),
                new SubNavLink("سجل التدقيق", "Audit log", "/audit-log", "audit log سجل تدقيق", "shield-check",
                    RequiredFeature: Feature.AuditLog),
                new SubNavSeparator(),
                new SubNavGroupHeader("الضرائب والإقفال", "Taxes & closing"),
                new SubNavLink("الضرائب", "Taxes", "/tax/vat-return",
                    "vat wht income compliance ضريبة دخل خصم تقويم صحة", "percent",
                    AliasUrls: new[]
                    {
                        "/tax/income-tax-return",
                        "/compliance/calendar", "/compliance/health",
                    }),
                new SubNavLink("الفاتورة الإلكترونية (ETA)", "E-invoicing (ETA)", "/eta-dashboard",
                    "eta inbox export wizard inspection لوحة صندوق فحص", "activity",
                    AliasUrls: new[]
                    {
                        "/eta-inbox", "/eta-export", "/eta-wizard", "/inspection-bundle",
                    }),
                new SubNavLink("الإقفال", "Closing", "/cockpit",
                    "closing cockpit year-end period lock approvals إقفال قفل تسوية", "calendar-range",
                    AliasUrls: new[]
                    {
                        "/year-end-close", "/approvals",
                    },
                    RequiredFeature: Feature.ClosingCockpit),
                new SubNavSeparator(),
                new SubNavGroupHeader("التقارير", "Reports"),
                new SubNavLink("التقارير المحاسبية", "Accounting reports", "/reports/trial-balance",
                    "trial balance gl pnl balance sheet cash flow cost center ميزان دفتر دخل ميزانية تدفقات", "bar-chart",
                    AliasUrls: new[]
                    {
                        "/reports/general-ledger", "/reports/profit-loss",
                        "/reports/balance-sheet", "/reports/cash-flow", "/reports/cost-centers",
                    },
                    RequiredFeature: Feature.TrialBalance),
                new SubNavLink("التقارير الضريبية", "Tax reports", "/reports/vat-monthly",
                    "vat monthly turnover taxable income penalty shield شهري أعمال دخل غرامات", "percent",
                    AliasUrls: new[]
                    {
                        "/reports/turnover-tax", "/reports/taxable-income", "/penalty-shield",
                    }),
            }),

        new AppShellModule(ContactsId, "users", "جهات الاتصال", "Contacts", "/customers", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("العملاء", "Customers", "/customers", "customer عميل", "user"),
                new SubNavLink("الموردين", "Suppliers", "/suppliers", "supplier مورد", "building"),
                new SubNavSeparator(),
                new SubNavLink("إدارة العلاقات (CRM)", "CRM", "/leads",
                    "leads projects routes my dashboard فرص مشاريع جولات لوحتي", "target",
                    AliasUrls: new[]
                    {
                        "/projects", "/routes", "/dashboards/me",
                    }),
                new SubNavLink("الجداول الزمنية", "Timesheets", "/timesheets/me",
                    "timesheet team hours جدول ساعات فريق", "clock",
                    AliasUrls: new[] { "/timesheets/team" }),
            }),

        new AppShellModule(SettingsId, "settings", "الإعدادات", "Settings", "/settings", HasSubNav: true,
            SubNavEntries: new SubNavEntry[]
            {
                new SubNavLink("لوحة الإعدادات", "Settings home", "/settings", "settings إعدادات", "settings"),
                new SubNavLink("بيانات الشركة", "Company info", "/settings/company", "company شركة", "building"),
                new SubNavSeparator(),
                new SubNavGroupHeader("التهيئة", "Configuration"),
                new SubNavLink("إعداد الضرائب", "Tax setup", "/settings/tax-periods",
                    "fiscal year tax period vat wht payment terms positions ضرائب فئات شروط مواقف", "calendar-range",
                    AliasUrls: new[]
                    {
                        "/settings/fiscal-year", "/settings/vat-categories",
                        "/settings/wht-categories", "/settings/payment-terms",
                        "/settings/fiscal-positions",
                    }),
                new SubNavLink("نقدية وبنوك", "Money & banks", "/settings/payment-methods",
                    "payment method cash accounts currencies opening balances نقدي خزائن بنوك عملات افتتاحي", "wallet",
                    AliasUrls: new[]
                    {
                        "/settings/cash-accounts", "/currencies", "/settings/opening-balances",
                    },
                    RequiredFeature: Feature.MultiCashbox),
                new SubNavLink("التسعير والمبيعات", "Pricing & sales", "/settings/quotation-templates",
                    "quotation pricelist sales teams قوالب أسعار فرق", "tag",
                    AliasUrls: new[]
                    {
                        "/settings/pricelists", "/settings/sales-teams",
                    }),
                new SubNavLink("فئات المصروفات", "Expense categories", "/settings/expense-categories", "expense category فئة", "tag"),
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
        new QuickAction("جدولي الزمني", "My timesheet", "/timesheets/me", "clock"),
        new QuickAction("تكلفة استيراد جديدة", "New landed cost", "/landed-costs/new", "package"),
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

        // Prefix match against each module's sub-nav links + alias urls
        // (the latter let a consolidated entry like "Accounting reports"
        // claim every sibling /reports/* page even though the entry's
        // canonical url is just /reports/trial-balance).
        foreach (var module in All)
        {
            foreach (var entry in module.SubNavEntries)
            {
                if (entry is not SubNavLink link) continue;
                if (Matches(path, link.Url)) return module.Id;
                if (link.AliasUrls is { } aliases)
                {
                    foreach (var alias in aliases)
                    {
                        if (Matches(path, alias)) return module.Id;
                    }
                }
            }
        }

        return DashboardId;
    }

    private static bool Matches(string path, string url) =>
        path.Equals(url, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(url.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns all sub-nav links across all modules — used by the
    /// Command Palette to provide searchable screen navigation.
    /// Skips entries the current edition can't access so search hits
    /// don't surface locked screens.
    /// </summary>
    public static IEnumerable<(string Label, string Url, string ModuleLabel, string IconName)> GetAllSearchableItems(bool arabic = true)
    {
        foreach (var module in All)
        {
            if (!module.IsAllowed()) continue;
            foreach (var entry in module.SubNavEntries)
            {
                if (entry is SubNavLink link && link.IsAllowed())
                {
                    var label = arabic ? link.LabelAr : link.LabelEn;
                    var modLabel = arabic ? module.LabelAr : module.LabelEn;
                    yield return (label, link.Url, modLabel, link.IconName);
                }
            }
        }
    }
}
