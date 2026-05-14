namespace EgyptTax.Web.Shared.AdminPanel;

/// <summary>
/// Gux.13 — single source of truth for the admin-panel tab list.
/// Used by <c>AdminPanel.razor</c> to render the tab bar, by the
/// old-URL redirect map to know where to bounce direct-link URLs,
/// and by the role-filter helper to decide which tabs a given user
/// can see.
/// </summary>
public static class AdminPanelTabs
{
    public sealed record Tab(
        string Slug,
        string Icon,
        string Ar,
        string En,
        AdminTabRole MinimumRole);

    public static readonly Tab[] All =
    {
        new("company-profile",   "building",  "بيانات الشركة",       "Company Profile",     AdminTabRole.AccountantViewAdminEdit),
        new("tax-config",        "percent",   "الإعدادات الضريبية",   "Tax Configuration",   AdminTabRole.AccountantViewAdminEdit),
        new("eta-integration",   "link",      "ربط منظومة ETA",       "ETA Integration",     AdminTabRole.AdminOnly),
        new("invoice-settings",  "file-text", "إعدادات الفواتير",     "Invoice Settings",    AdminTabRole.AccountantEditAdminEdit),
        new("email-settings",    "mail",      "البريد الإلكتروني",    "Email Settings",      AdminTabRole.AdminOnly),
        new("user-management",   "users",     "إدارة المستخدمين",     "User Management",     AdminTabRole.AdminOnly),
        new("license",           "key",       "الترخيص",             "License",             AdminTabRole.AccountantViewAdminEdit),
        new("backup",            "save",      "النسخ الاحتياطي",      "Backup",              AdminTabRole.AdminOnly),
        new("notifications",     "bell",      "الإشعارات",           "Notifications",       AdminTabRole.AnyAuthenticated),
        new("sales-reps",        "users",     "مناديب المبيعات",      "Sales Reps",          AdminTabRole.AdminOnly),
        new("ai",                "zap",       "الذكاء الاصطناعي",     "AI (Claude)",         AdminTabRole.AdminOnly),
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
    /// given role-codes is allowed to see.</summary>
    public static IEnumerable<Tab> VisibleFor(IEnumerable<string> roleCodes)
    {
        var roles = new HashSet<string>(roleCodes, StringComparer.OrdinalIgnoreCase);
        var isAdmin       = roles.Contains("ADMIN");
        var isAccountant  = roles.Contains("ACCOUNTANT") || roles.Contains("BOOKKEEPER");

        foreach (var tab in All)
        {
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
