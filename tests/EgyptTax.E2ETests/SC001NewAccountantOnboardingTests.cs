using System.Diagnostics;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace EgyptTax.E2ETests;

/// <summary>
/// T253 / SC-001 — a brand-new accountant completes the
/// quickstart §7 path end-to-end in under 15 minutes (timed).
///
/// **Status (2026-05-08): SKELETON SHIPPED, TIMED FACT SKIPPED.**
/// Same blocker pattern as <see cref="BilingualRenderingTests"/>:
/// the harness needs either a hosted EgyptTax instance (env
/// <c>EGYPTTAX_E2E_URL</c>) or a TestServer-based factory shipping.
/// On top of that, this test needs a fresh-install starting state
/// (no documents posted, no master data beyond the seeded
/// Administrator) — the harness must drop + recreate the test DB
/// per run to satisfy the "brand-new accountant" precondition.
///
/// Step inventory (the canonical 12-step quickstart §7 from
/// <c>specs/008-egypt-tax-accounting/quickstart.md</c>) — each step
/// is a Playwright `await page.X(...)` block + an assertion. The
/// total wall time MUST land under <c>SC001Budget</c>.
///
/// <list type="number">
///   <item>Sign in with the seeded Administrator + temp password.</item>
///   <item>Force-change the password (prompted on first sign-in).</item>
///   <item>Enroll MFA — scan the TOTP QR + enter the 6-digit code.</item>
///   <item>Set company profile (legal name AR/EN, TIN, CR, address,
///   activity code, fiscal year).</item>
///   <item>Add a customer (TIN, name, address, default VAT category).</item>
///   <item>Add an item (code, name, default VAT category).</item>
///   <item>Add a VAT category if not seeded (Standard 14%
///   effective today).</item>
///   <item>Create a draft sales invoice → add 1 line at qty 1 +
///   unit price 10,000 EGP.</item>
///   <item>Post the invoice. Assert the invoice number is
///   <c>INV-{currentYear}-000001</c> + state = Posted.</item>
///   <item>Download the PDF; assert non-zero byte count + a
///   PDF magic-byte prefix.</item>
///   <item>Visit the ETA Dashboard; assert the new invoice
///   appears in either Pending or Submitted (mock client
///   typically resolves to Submitted within seconds).</item>
///   <item>Scan the QR seal endpoint <c>/api/v1/verify/{seal}</c>;
///   assert response = VALID with the matching grand total.</item>
/// </list>
///
/// Total budget: <see cref="SC001Budget"/>. The MVP target is
/// 15 min for a non-technical user; a Playwright bot should
/// land WELL under that — the budget exists to catch the case
/// where a UX regression makes a step take hundreds of seconds
/// (e.g. a slow report query blocking the cockpit redirect).
/// </summary>
public class SC001NewAccountantOnboardingTests : IAsyncLifetime
{
    private const string SkipReason =
        "Acceptance harness — SC-001 measures a real human's wall-clock "
        + "through the 12-step quickstart, so the test needs a real "
        + "DaftarX deployment (EGYPTTAX_E2E_URL), a fresh-install DB "
        + "drop+recreate per run, and Playwright Chromium installed. "
        + "Skipped in CI; not a candidate for the in-process "
        + "EgyptTaxE2EFactory pattern (the budget gate is meaningless "
        + "against SQLite-in-memory).";

    private static readonly TimeSpan SC001Budget = TimeSpan.FromMinutes(15);

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
        catch { /* harness not yet wired — see SkipReason */ }
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact(Skip = SkipReason)]
    public async Task SC001_FullQuickstart_CompletesUnder15Min()
    {
        var sw = Stopwatch.StartNew();

        // PLANNED: 12-step Playwright sequence per the inventory in
        // the class docstring. Each step asserts its own success
        // before proceeding (no late-fail amplification — a broken
        // step 3 stops here, not at step 12).
        await Task.CompletedTask;

        sw.Stop();
        sw.Elapsed
            .Should()
            .BeLessThan(
                SC001Budget,
                because: $"SC-001 — a new accountant MUST complete the 12-step quickstart in under {SC001Budget.TotalMinutes:F0} min; "
                    + $"actual {sw.Elapsed.TotalSeconds:F1} s"
            );
    }
}
