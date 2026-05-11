using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace EgyptTax.E2ETests;

/// <summary>
/// T252 / R-11 / R-19 — bilingual-rendering E2E. For every navigable
/// page in the MVP, visit it with the AR culture cookie set + assert
/// <c>html[dir="rtl"]</c> + Arabic labels render; visit with the EN
/// cookie + assert <c>html[dir="ltr"]</c> + English labels.
///
/// **Status (2026-05-08): SKELETON SHIPPED, FACTS SKIPPED.** The
/// fixture cannot run GREEN until either (a) a hosted EgyptTax
/// instance is reachable on a known URL (then set
/// <see cref="BaseUrl"/> via env var <c>EGYPTTAX_E2E_URL</c>), or
/// (b) a TestServer-based <c>WebApplicationFactory&lt;Program&gt;</c>
/// helper is shipped to spin up an in-process host with a
/// SqlServer-fixture DB seed.
///
/// Until then, the skeleton:
/// <list type="number">
///   <item>Documents the canonical page inventory the bilingual
///   gate checks (kept in sync with <c>MainLayout.razor</c>).</item>
///   <item>Pins the Playwright bootstrap shape (mirrors T084's
///   <c>US1_SalesInvoiceQuickstartTests</c>).</item>
///   <item>Acts as a CI gate — once the Skip is removed and the
///   harness is wired, R-11 (RTL parity) + R-19 (Egyptian
///   terminology rendering) are observably satisfied for every
///   page in the table below.</item>
/// </list>
///
/// Page inventory (one row per public route in <c>MainLayout.razor</c>'s
/// nav at the time of writing — when a new nav link lands, add a row):
/// <list type="bullet">
///   <item><c>/</c></item>
///   <item><c>/customers</c>, <c>/suppliers</c>, <c>/items</c></item>
///   <item><c>/expense-categories</c></item>
///   <item><c>/invoices</c>, <c>/purchase-invoices</c>, <c>/expenses</c></item>
///   <item><c>/reports/vat-monthly</c>, <c>/reports/taxable-income</c>,
///   <c>/reports/trial-balance</c></item>
///   <item><c>/cockpit</c>, <c>/eta-dashboard</c>, <c>/audit-log</c></item>
///   <item><c>/approvals</c>, <c>/inspection-bundle</c></item>
///   <item><c>/settings/company</c>, <c>/settings/tax-periods</c>,
///   <c>/settings/vat-categories</c>,
///   <c>/settings/expense-categories</c>,
///   <c>/settings/chart-of-accounts</c>,
///   <c>/settings/fiscal-year</c>,
///   <c>/settings/payment-methods</c></item>
///   <item><c>/firm-portal</c>, <c>/firm-portal/switch</c></item>
///   <item><c>/wht</c>, <c>/wht/form41</c>, <c>/wht/form41/new</c>,
///   <c>/settings/wht-categories</c></item>
/// </list>
/// </summary>
public class BilingualRenderingTests : IAsyncLifetime
{
    private const string SkipReason =
        "Acceptance harness — drives a real browser against a running "
        + "DaftarX instance at EGYPTTAX_E2E_URL. Skipped in CI; run "
        + "locally after `dotnet run --project src/EgyptTax.Web` + "
        + "`playwright install chromium`. EgyptTaxE2EFactory is also "
        + "an option if/when these tests get rewritten to drive an "
        + "in-process server (SQLite-in-memory shape, see "
        + "HealthSmokeTests).";

    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("EGYPTTAX_E2E_URL") ?? "https://localhost";

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true }
            );
        }
        catch
        {
            // Browser binaries may not be installed in CI yet — failing
            // the lifecycle would mask the Skip outcome.
        }
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact(Skip = SkipReason)]
    public async Task EveryNavPage_RendersRtl_WhenArabicCultureCookieSet()
    {
        // PLANNED: for each route in the inventory above, set the
        // AspNetCore.Culture cookie to "c=ar-EG|uic=ar-EG", load the
        // page, assert html[dir="rtl"] + html[lang="ar"] + the
        // expected Arabic title text.
        await Task.CompletedTask;
    }

    [Fact(Skip = SkipReason)]
    public async Task EveryNavPage_RendersLtr_WhenEnglishCultureCookieSet()
    {
        // PLANNED: same as above but with culture cookie
        // "c=en-US|uic=en-US"; assert html[dir="ltr"] +
        // html[lang="en"] + the expected English title text.
        await Task.CompletedTask;
    }

    [Fact(Skip = SkipReason)]
    public async Task LanguageToggle_PersistsAcrossNavigation()
    {
        // PLANNED: switch to AR via the toggle, navigate to a
        // different page, assert the page still renders RTL + AR
        // (cookie persisted). Reverse for EN.
        await Task.CompletedTask;
    }
}
