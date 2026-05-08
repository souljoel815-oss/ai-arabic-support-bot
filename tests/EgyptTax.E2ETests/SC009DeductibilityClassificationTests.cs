using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace EgyptTax.E2ETests;

/// <summary>
/// T254 / SC-009 — a non-technical user correctly classifies
/// deductible vs non-deductible on at least 9 of 10 sample
/// scenarios using only the in-product hints + tooltips.
///
/// **Status (2026-05-08): SKELETON SHIPPED, FACT SKIPPED.** Same
/// harness blocker as the other E2E tests — needs a reachable
/// instance + a fresh-install state. On top of that, this test
/// needs a deterministic seed of the 10 sample scenarios in
/// the database so the classification UI presents them in a
/// predictable order with predictable correct answers.
///
/// Sample scenario inventory (drawn from the spec's Egyptian-tax
/// canonical examples — the seeder MUST land these exact rows so
/// the assertion can key off scenario id):
/// <list type="number">
///   <item>Office rent for the business premises — DEDUCTIBLE</item>
///   <item>Owner's personal car insurance — NON-DEDUCTIBLE</item>
///   <item>Salaries paid to staff — DEDUCTIBLE</item>
///   <item>Customer-entertainment dinner above the
///   per-event cap — NON-DEDUCTIBLE</item>
///   <item>Internet + utilities for the office — DEDUCTIBLE</item>
///   <item>Owner's personal vacation — NON-DEDUCTIBLE</item>
///   <item>Inventory purchase from a registered supplier
///   with a tax invoice — DEDUCTIBLE (input VAT recoverable)</item>
///   <item>Inventory purchase from an unregistered supplier
///   with no tax invoice — DEDUCTIBLE for income tax (cost) but
///   the input VAT is NON-recoverable per FR-020</item>
///   <item>Fine paid to the tax authority — NON-DEDUCTIBLE</item>
///   <item>Bank fees on the operating account — DEDUCTIBLE</item>
/// </list>
///
/// The test drives a non-technical user through the
/// classification UI. For each scenario the user reads the
/// description + makes a deductible/non-deductible choice; the
/// in-product hints ("Personal expenses are non-deductible per
/// Egyptian Income Tax Law Art. X") are the only guidance they
/// see — no external reference material.
///
/// Pass criterion: ≥ 9 of 10 correct. SC-009's 90% bar is the
/// usability floor for the FR-015 deductible-classification UX —
/// dropping below it on a regression means the hints have
/// degraded to the point a non-technical user can no longer
/// be trusted to classify correctly.
/// </summary>
public class SC009DeductibilityClassificationTests : IAsyncLifetime
{
    private const string SkipReason =
        "Blocked: requires (a) reachable EgyptTax instance via env EGYPTTAX_E2E_URL, "
        + "(b) deterministic seed of the 10 sample scenarios with stable ids, "
        + "(c) Microsoft.Playwright browsers on the runner. The skeleton documents "
        + "the canonical scenario inventory + the 9-of-10 pass criterion; remove "
        + "the Skip when the harness + seeded scenarios land.";

    private const int RequiredCorrect = 9;
    private const int TotalScenarios = 10;

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
    public async Task NonTechnicalUser_Classifies_AtLeast9Of10_Correctly()
    {
        // PLANNED: for each of the 10 seeded scenarios, drive the
        // classification UI as a non-technical user (read the
        // description + the in-product hint, pick deductible /
        // non-deductible, click submit). Compare each pick against
        // the canonical correct answer; tally correct-count.

        var correctCount = 0;
        // for (var i = 0; i < TotalScenarios; i++) {
        //     var (scenarioId, expected) = ScenarioInventory[i];
        //     var picked = await ClassifyScenario(page, scenarioId);
        //     if (picked == expected) correctCount++;
        // }

        await Task.CompletedTask;

        correctCount
            .Should()
            .BeGreaterThanOrEqualTo(
                RequiredCorrect,
                because: $"SC-009 — a non-technical user MUST correctly classify "
                    + $"{RequiredCorrect} of {TotalScenarios} scenarios using only the "
                    + "in-product hints; falling below this floor signals the FR-015 "
                    + "deductibility UX has regressed past the usability bar"
            );
    }
}
