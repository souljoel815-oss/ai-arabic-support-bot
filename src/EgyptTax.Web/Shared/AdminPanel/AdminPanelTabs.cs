using EgyptTax.Web.Licensing;

namespace EgyptTax.Web.Shared.AdminPanel;

/// <summary>
/// Gux.13 — single source of truth for the admin-panel tab list.
/// Used by <c>AdminPanel.razor</c> to render the tab bar, by the
/// old-URL redirect map to know where to bounce direct-link URLs,
/// and by the role-filter helper to decide which tabs a given user
/// can see.
///
/// v5.1 — each tab can also carry a <c>RequiredFeature</c>
/// (a <see cref="Feature"/> constant). When non-null, the tab is
/// hidden for editions that don't include the feature — independent
/// of role gating. Role gates the user; feature gates the install.
/// </summary>
public static class AdminPanelTabs
{
    public sealed record Tab(
        string Slug,
        string Icon,
        string Ar,
        string En,
        AdminTabRole MinimumRole,
        string? RequiredFeature = null);

    public static readonly Tab[] All =
    {
        new("company-profile",   "building",  "بيانات الشركة",       "Company Profile",     AdminTabRole.AccountantViewAdminEdit),
        new("tax-config",        "percent",   "الإعدادات الضريبية",   "Tax Configuration",   AdminTabRole.AccountantViewAdminEdit),
        new("eta-integration",   "link",      "ربط منظومة ETA",       "ETA Integration",     AdminTabRole.AdminOnly),
        new("invoice-settings",  "file-text", "إعدادات الفواتير",     "Invoice Settings",    AdminTabRole.AccountantEditAdminEdit),
        new("email-settings",    "mail",      "البريد الإلكتروني",    "Email Settings",      AdminTabRole.AdminOnly,
            RequiredFeature: Feature.SmtpDirectEmail),
        new("user-management",   "users",     "إدارة المستخدمين",     "User Management",     AdminTabRole.AdminOnly,
            RequiredFeature: Feature.MultiUser),
        new("license",           "key",       "الترخيص",             "License",             AdminTabRole.AccountantViewAdminEdit),
        new("backup",            "save",      "النسخ الاحتياطي",      "Backup",              AdminTabRole.AdminOnly,
            RequiredFeature: Feature.AutoBackup),
        new("notifications",     "bell",      "الإشعارات",           "Notifications",       AdminTabRole.AnyAuthenticated),
        new("sales-reps",        "users",     "مناديب المبيعات",      "Sales Reps",          AdminTabRole.AdminOnly),
        new("ai",                "zap",       "الذكاء الاصطناعي",     "AI (Claude)",         AdminTabRole.AdminOnly,
            RequiredFeature: Feature.ArabicAiAssistant),
        new("api-keys",          "key",       "مفاتيح API",          "API Keys",            AdminTabRole.AdminOnly),
        new("about",             "info",      "حول ومساعدة",         "About & Support",     AdminTabRole.AnyAuthenticated),
    };

    /// <summary>Old `/settings/X` URL → matching tab slug. Used by
    /// the redirect endpoint at the route level (so direct links to
    /// the old URLs still land on the right place).</summary>
    public static readonly IReadOnlyDictionary<string, string> OldRouteToTab =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["company"]            = "company-profile",
            ["tax-periods"]        = "tax-config",
            ["vat-categories"]     = "tax-config",
            ["wht-categories"]     = "tax-config",
            ["fiscal-year"]        = "tax-config",
            ["payment-methods"]    = "invoice-settings",
            ["cash-accounts"]      = "invoice-settings",
            ["opening-balances"]   = "invoice-settings",
            ["chart-of-accounts"]  = "invoice-settings",
            ["expense-categories"] = "invoice-settings",
            ["referrals"]          = "about",  // Refer-a-friend lives elsewhere; redirect to About
        };

    /// <summary>Filter the tab list to the ones a user with the
    /// given role-codes is allowed to see AND whose required feature
    /// (if any) is included in the current edition. Feature gate is
    /// applied first because a missing edition entitlement should
    /// hide the tab even from admins — the install simply doesn't
    /// have that capability.</summary>
    public static IEnumerable<Tab> VisibleFor(IEnumerable<string> roleCodes)
    {
        var roles = new HashSet<string>(roleCodes, StringComparer.OrdinalIgnoreCase);
        var isAdmin       = roles.Contains("ADMIN");
        var isAccountant  = roles.Contains("ACCOUNTANT") || roles.Contains("BOOKKEEPER");

        foreach (var tab in All)
        {
            // Edition gate first — no role override for missing features.
            if (tab.RequiredFeature is not null && !EditionGate.Allows(tab.RequiredFeature))
                continue;

            if (tab.MinimumRole == AdminTabRole.AnyAuthenticated)
            {
                yield return tab;
                continue;
            }
            if (isAdmin)
            {
                yield return tab;
                continue;
            }
            if (isAccountant && tab.MinimumRole != AdminTabRole.AdminOnly)
            {
                yield return tab;
                continue;
            }
            // Cashier / View-only see only the AnyAuthenticated tabs.
        }
    }
}

/// <summary>
/// Gux.13 — coarse-grained per-tab visibility rule. Server-side
/// enforcement is also applied in each tab's onInit (this enum is
/// just for the sidebar render decision).
/// </summary>
public enum AdminTabRole
{
    /// <summary>Visible + read-only for everyone signed in.</summary>
    AnyAuthenticated,
    /// <summary>Admin can edit; Accountant/Bookkeeper can view; nobody else.</summary>
    AccountantViewAdminEdit,
    /// <summary>Admin + Accountant can edit; nobody else.</summary>
    AccountantEditAdminEdit,
    /// <summary>Admin only — others can't see the tab at all.</summary>
    AdminOnly,
}
