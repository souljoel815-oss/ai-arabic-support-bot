using System.Reflection;
using Xunit.Sdk;

namespace EgyptTax.IntegrationTests.Performance;

/// <summary>
/// T255 — nightly performance suite harness. Indexes the SC-perf
/// gates that ship across the integration suite + asserts each one
/// carries the <c>[Trait("Category","Slow")]</c> tag so the
/// canonical nightly CI invocation
/// (<c>--filter "Category=Slow"</c>) catches them all.
///
/// This file is the single discoverable "what runs nightly" map.
/// Adding a new perf test to the table below + tagging it Slow is
/// the contract; the meta-assertion here fails loudly if either
/// half drifts.
///
/// Canonical CI invocation:
/// <code>
/// dotnet test tests/EgyptTax.IntegrationTests \
///   --filter "Category=Slow" \
///   --logger "trx;LogFileName=nightly-perf.trx"
/// </code>
///
/// Coverage (one row per spec-defined success criterion):
/// <list type="table">
///   <listheader>
///     <term>SC</term> <description>Test class — Budget</description>
///   </listheader>
///   <item><term>SC-002</term><description>
///     <c>Reports.ReportPerformanceTests</c> — VAT-monthly +
///     Taxable-income each &lt; 5 s p95 at 5k docs / 25 concurrent
///     users (T240).
///   </description></item>
///   <item><term>SC-006</term><description>
///     <c>Numbering.ConcurrentPostingStressTests</c> — zero gaps,
///     zero duplicates across 500 concurrent posts spanning a
///     fiscal-year boundary.
///   </description></item>
///   <item><term>SC-010</term><description>
///     <c>Audit.AuditChainPerformanceTests</c> — chain verification
///     of 1,000,000 entries in &lt; 30 s.
///   </description></item>
///   <item><term>SC-012</term><description>
///     <c>Compliance.EtaDashboardPerfTests</c> — ETA dashboard
///     deadline-finder &lt; 2 s on 50k posted documents.
///   </description></item>
///   <item><term>SC-013</term><description>
///     <c>EgyptTax.ContractTests.Verification.QrSealTests</c> —
///     QR-seal round-trip + tamper detection across 100 random
///     documents (perf budget &lt; 1 s/doc; lives in the contract
///     suite because the seal is a pure-function codec, not a DB
///     round-trip).
///   </description></item>
///   <item><term>SC-014</term><description>
///     Two halves: <c>FirmPortal.CompanySwitchPerfTests</c> —
///     server-side firm-user lookup across two installations
///     &lt; 1 s (3 round-trips inside the budget; T217). And
///     <c>Inspection.BundleEndToEndTests</c> — bundle for a
///     5k-document quarter built in &lt; 5 min, re-verified by
///     the bundled script in &lt; 60 s (gated by
///     <c>EGYPTTAX_RUN_PERF=1</c> opt-in env so the seed cost
///     doesn't surprise local dev runs).
///   </description></item>
/// </list>
///
/// SC-014's CompanySwitchPerfTests is intentionally NOT
/// Slow-tagged — its &lt; 4 s wall time fits comfortably in the
/// default integration suite, and pinning it there means the
/// daily developer loop catches a regression on the firm-user
/// lookup before it hits nightly. Every OTHER SC-perf test IS
/// Slow-tagged so a 50k seed doesn't drag the dev loop.
/// </summary>
public class NightlyPerfSuite
{
    /// <summary>
    /// Meta-assertion: every test class flagged here as part of
    /// the nightly perf suite MUST carry
    /// <c>[Trait("Category","Slow")]</c>. If a future contributor
    /// adds a perf test + forgets the trait, the dev loop still
    /// runs it (good — a regression won't ship), but the nightly
    /// filter would silently miss it (bad — we'd lose the perf
    /// signal). This test catches that drift.
    /// </summary>
    [Fact]
    public void EveryNightlyPerfClass_CarriesSlowTrait()
    {
        var nightlyClasses = new[]
        {
            typeof(EgyptTax.IntegrationTests.Reports.ReportPerformanceTests),                  // SC-002
            typeof(EgyptTax.IntegrationTests.Numbering.ConcurrentPostingStressTests),          // SC-006
            typeof(EgyptTax.IntegrationTests.Audit.AuditChainPerformanceTests),                // SC-010
            typeof(EgyptTax.IntegrationTests.Compliance.EtaDashboardPerfTests),                // SC-012
            typeof(EgyptTax.IntegrationTests.Inspection.BundleEndToEndTests),                  // SC-014 (bundle half)
        };

        var missing = nightlyClasses
            .Where(t => !HasSlowTrait(t))
            .Select(t => t.FullName!)
            .ToList();

        missing.Should().BeEmpty(
            because: "every nightly perf class MUST carry [Trait(\"Category\",\"Slow\")] so the "
                + "`dotnet test --filter \"Category=Slow\"` nightly invocation catches it; "
                + "missing classes would silently disappear from the perf signal");
    }

    private static bool HasSlowTrait(Type t)
    {
        // TraitAttribute exposes its (name, value) pair only via the
        // constructor args — read them from CustomAttributeData
        // instead of the runtime instance.
        return t.GetCustomAttributesData()
            .Where(a => a.AttributeType == typeof(TraitAttribute))
            .Any(a => a.ConstructorArguments.Count == 2
                && a.ConstructorArguments[0].Value as string == "Category"
                && a.ConstructorArguments[1].Value as string == "Slow");
    }
}
