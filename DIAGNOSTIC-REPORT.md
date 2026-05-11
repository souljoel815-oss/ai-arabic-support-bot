# DaftarX — Diagnostic Sweep

> Read-only audit of the codebase, test suites, and build state.
> No code changes were made during this run. Each finding has a
> severity and a recommended next step.

**Run on:** 2026-05-11
**Tree state:** branch `008-egypt-tax-accounting`, **231 files**
with uncommitted changes (see Finding 11).

---

## Executive summary

| Suite | Pass | Fail | Skip | Verdict |
|---|---|---|---|---|
| **Unit tests** | 275 | 0 | 0 | ✅ Green |
| **Integration tests** | 212 | 0 | 0 | ✅ Green (6 m 45 s, Testcontainers SQL) |
| **Contract tests** | 29 | 11 | 0 | ❌ **Broken** (test-host crash; 1 real root cause) |
| **E2E tests** | 0 | 2 | 15 | ❌ Same root cause + Playwright harness not wired |
| **Solution build** | — | — | — | ✅ 0 errors, 0 warnings (after stale testhost cleared) |

**Bottom line:** The Domain / Application / Infrastructure layers
are healthy. The two test-host-based suites (Contract + E2E) share
**one root cause** — the EF Core InMemory provider can't handle the
`ComplexProperty<ArabicEnglishText>` mappings the model uses. Fixing
that one bug unlocks 11 contract tests + 2 E2E tests + makes the
remaining 15 skipped E2E tests at least theoretically runnable.

Beyond the test issue, two notable structural problems:

1. **Foreign code in `src/`** — an entirely separate `trading_bot`
   Python project sits untracked alongside DaftarX (Finding 10).
2. **Massive uncommitted WIP** — 231 changed files across the
   tree (Finding 11).

Everything else (warnings, secret leakage, async-correctness, code
smells) is **clean**. Eight findings total.

---

## Finding 1 — Contract test factory uses EF InMemory; model uses `ComplexProperty`. Hard crash on first query. 🔴 **High**

**Where:**
[`tests/EgyptTax.ContractTests/Api/OpenApiAlignmentTests.cs:87-103`](tests/EgyptTax.ContractTests/Api/OpenApiAlignmentTests.cs#L87-L103)

```csharp
public sealed class EgyptTaxContractTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // ... remove SQL options ...
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("ContractTests"));
        });
    }
}
```

**Symptom:** Both `OpenApiAlignmentTests` throw at app boot:

```
System.Collections.Generic.KeyNotFoundException :
The given key 'Property: Role.Name#ArabicEnglishText.Arabic
(string) Required MaxLength(100)' was not present in the
dictionary.
   at PortableFirstRun.EnsureAdminUserAsync (Tools/PortableFirstRun.cs:81)
   at Program.<Main>$ (Program.cs:1276)
```

**Root cause:** The EF8 model uses `b.ComplexProperty(...)` to
flatten `ArabicEnglishText` / `MoneyEgp` into multi-column shapes
(e.g.,
[`PostalAddressConfiguration`](src/EgyptTax.Infrastructure/Persistence/CompanyConfiguration.cs),
the `Role.Name` config). EF Core 8 InMemory provider does NOT
support ComplexProperty — it crashes on the first read.

**Collateral damage:** When this fails inside `IClassFixture` boot,
xUnit's parallel runner destabilises the next 9 PDF tests in the
shared host. **They pass in isolation but fail in the suite.**

**Fix options:**
- (preferred) Replace `UseInMemoryDatabase` with SQLite (in-memory:
  `Filename=:memory:`). SQLite supports ComplexProperty.
- Alternative: switch the contract-test factory to the existing
  `SqlServerFixture` (Testcontainers) — already proven by the 212
  passing integration tests.
- Alternative: stub `EnsureAdminUserAsync` out for tests via a
  configuration flag.

**Effort:** 1 hour. Unlocks 11 tests immediately.

---

## Finding 2 — Same root cause kills 2 E2E `HealthSmokeTests`. 🔴 **High**

**Where:**
[`tests/EgyptTax.E2ETests/HealthSmokeTests.cs`](tests/EgyptTax.E2ETests/HealthSmokeTests.cs)

**Symptom:** identical `KeyNotFoundException` stack trace from
`Role.Name#ArabicEnglishText.Arabic`. The E2E `HealthSmokeTests`
uses the same `WebApplicationFactory + InMemory` shape as the
contract tests.

**Fix:** same as Finding 1. Sharing one test-server fixture across
both test projects would have prevented the duplicated bug.

---

## Finding 3 — 15 E2E tests `Skip` with stale or infra-blocked reasons. 🟡 **Medium**

**Where:** four test files, all in `tests/EgyptTax.E2ETests/`:

| File | Skipped tests | Skip reason |
|---|---|---|
| `Stories/US1_SalesInvoiceQuickstartTests.cs` | 10 | "Blocked: requires US1 implementation tasks T085+" — **stale**, those tasks shipped during Wave 1 (login, MFA, invoice editor are all real) |
| `BilingualRenderingTests.cs` | 3 | Playwright harness not wired |
| `SC001NewAccountantOnboardingTests.cs` | 1 | Needs `EGYPTTAX_E2E_URL` env + Playwright browsers + DB drop-recreate fixture |
| `SC009DeductibilityClassificationTests.cs` | 1 | Same as SC001 |

**Recommendation:**
- **US1 Quickstart tests** — Skip text is wrong; the underlying
  pages exist. Either un-skip and let them fail (forcing real
  fixes) or update the Skip text to the *real* current blocker
  (Playwright fixture).
- **SC001 / SC009 / Bilingual** — legitimate (acceptance harnesses
  need a deployed instance). Move them into a separate `acceptance`
  test project so they don't show up as "skipped" in CI; tag the
  CI job that runs them as `acceptance-only`.

**Effort:** 30 min to retag.

---

## Finding 4 — One `async void` in a Razor page event handler. 🟢 **Low / acceptable**

**Where:**
[`src/EgyptTax.Web/Pages/Compliance/EtaDashboard.razor:132`](src/EgyptTax.Web/Pages/Compliance/EtaDashboard.razor#L132)

```csharp
private async void OnStatusChanged(object? sender, EtaStatusChangedEvent e)
```

**Verdict:** legitimate use case (event-handler signature requires
void). An unhandled exception inside here will crash the Blazor
circuit, but the body looks defensive.

**Optional hardening:** wrap the body in try/catch + log to Serilog
so a thrown exception updates a non-fatal toast instead of killing
the SignalR connection.

---

## Finding 5 — No `.Result` / `GetAwaiter().GetResult()` deadlocks in Web. 🟢 **All clear**

Greped the entire Web layer for sync-over-async patterns —
**zero matches**. Async hygiene is correct.

---

## Finding 6 — No empty `catch` swallows in source code. 🟢 **All clear**

Scanned every `catch` block in `src/`. The handful that DO swallow
(`/* best-effort */` in licensing storage + installer scripts) are
all annotated and intentional (deletion of files during reset where
failure is non-fatal). No production handlers silently eat
exceptions.

---

## Finding 7 — No `TODO` / `FIXME` / `HACK` / `XXX` markers in `src/EgyptTax.*`. 🟢 **All clear**

Surprising and good. Either every TODO got resolved or none were
ever written. Combined with **zero** `NotImplementedException`
sites, the production code is intentionally complete.

---

## Finding 8 — No secret-shaped tokens in committed config. 🟢 **All clear**

- `appsettings.Development.json` uses `Trusted_Connection=True`
  (no committed passwords).
- `vendor-keys.json` (private signing key) is correctly in
  `.gitignore`.
- `license.token`, `share*.bin`, `share*.mask` all in `.gitignore`.
- `licenses/` folder (per-customer issued tokens) is in
  `.gitignore`.

The only "password" hit in source was the documented
**bootstrap admin password** (`Admin@2026!`) in the user guide and
two integration tests — that's the seeded credential the operator
must change on first login, not a leaked secret.

---

## Finding 9 — `publish/` directory committed (or at least present in working tree). 🟡 **Medium**

```
publish/appsettings.Development.json
publish/EgyptTax.Web/appsettings.Development.json
```

**Risk:** build artefacts in source tree confuse future diffs and
can leak environment-specific paths into the repo. The repo
already publishes via dedicated scripts to `installer/` or
`installer-inno/` — `publish/` looks left over from a manual
`dotnet publish`.

**Recommended action:** add `publish/` to `.gitignore` and delete
the folder. Verify nothing else depends on it first
(`grep -rn "publish/" src/ tests/ specs/`).

---

## Finding 10 — Foreign Python project (`trading_bot`) sitting inside `src/`. 🟠 **Medium-high**

```
src/003-crypto-trading-bot/      (spec.md, tasks.md, ...)
src/trading_bot/                 (Python package — alerts, config, exchanges, ...)
src/trading_bot.egg-info/        (Python build metadata)
src/src.zip                      (??)
```

All four are **untracked** (`git status` shows `??`). They are a
completely different project (a crypto trading bot in Python) that
ended up extracted into DaftarX's `src/` tree. Likely cause: a
`.zip` archive of a different project was unzipped here by
mistake.

**Side effects observed during the sweep:**
- The risky-pattern grep returned many `password / secret / token`
  hits from this foreign tree, polluting the audit signal.
- Search engines / language servers index 2× as many files.
- A future `dotnet build src/` could attempt to compile or restore
  unrelated content.

**Recommended action:**
1. Confirm with the user this is not part of DaftarX.
2. Move it out — `mv src/trading_bot ~/ProjectX/` (or wherever it
   belongs).
3. Delete `src/src.zip` and `src/003-crypto-trading-bot/`.

---

## Finding 11 — 231 uncommitted file changes on the active branch. 🟠 **Medium-high**

The branch `008-egypt-tax-accounting` has 231 files in `git status`,
spanning every layer (Domain, Application, Infrastructure, Web,
Installer, tests). A sample:

```
 M src/EgyptTax.Domain/Documents/SupplierPaymentVoucher.cs
 M src/EgyptTax.Domain/Documents/CustomerReceiptVoucher.cs
 M src/EgyptTax.Domain/Eta/EtaSubmission.cs
 M src/EgyptTax.Domain/MasterData/Company.cs
 M src/EgyptTax.Domain/MasterData/Item.cs
 M src/EgyptTax.Infrastructure/Migrations/AppDbContextModelSnapshot.cs
 M src/EgyptTax.Installer/Product.wxs
 M src/EgyptTax.Installer/apply-config.ps1
 ...
```

**Risk:** A `git reset` / merge / cleanup at any point could lose
hours of Wave-4 work (P3.4 bank auto-match + P3.6 closing gate +
license-issuing script + USER-GUIDE).

**Recommended action:** chunked commits for the logical units:
- "P3.4 bank auto-match — domain + scorer + Hangfire job + UI"
- "P3.6 closing-readiness gate + force-lock audit"
- "License issuing scripts (Issue-License.ps1/.cmd)"
- "USER-GUIDE.md"
- "Test license init via ModuleInitializer"

Five commits, one branch push, no work in the danger zone.

---

## Build & dependency state

- **Build:** `dotnet build EgyptTax.sln` → 0 errors, 0 warnings
  (after stale testhost cleared).
- **Target framework:** .NET 8 (LTS) — current.
- **Package version policy:** `Directory.Packages.props` is checked
  in; centralised version management is healthy.
- **No analyzer downgrades** observed (no `<NoWarn>` blocks
  silencing CA/CS rules in any csproj).

---

## Test suite distribution

| Project | Tests | Notes |
|---|---|---|
| `EgyptTax.UnitTests` | 275 | Domain + Application logic, in-memory only |
| `EgyptTax.IntegrationTests` | 212 | Real SQL Server via Testcontainers; 6 m 45 s wall time |
| `EgyptTax.ContractTests` | 40 (29 pass, 11 fail) | PDF rendering + OpenAPI surface; **InMemory bug blocks 11** |
| `EgyptTax.E2ETests` | 17 (0 pass, 2 fail, 15 skip) | Playwright; needs deployed instance |

**Total passing:** 516. **Total broken by root cause #1:** 13.

---

## Priority action list

| # | Action | Effort | Unlocks |
|---|---|---|---|
| 1 | Fix Finding 1 (swap contract-test InMemory → SQLite-in-memory or share `SqlServerFixture`) | 1 h | 11 contract tests + 2 E2E health-smoke tests |
| 2 | Commit Wave-4 work in 5 chunks (Finding 11) | 30 min | Safety net on uncommitted changes |
| 3 | Move `src/trading_bot` out of the DaftarX tree (Finding 10) | 5 min | Clean working tree |
| 4 | Add `publish/` to `.gitignore` (Finding 9) | 2 min | Repo hygiene |
| 5 | Audit + retag E2E skip reasons (Finding 3) | 30 min | Honest test report |
| 6 | (Optional) Harden Razor event handler `async void` (Finding 4) | 10 min | Resilient SignalR circuit |

Estimated total: **~2 h 30 m**. After that, every suite either runs
green or has a documented external-infra requirement.

---

## What this sweep did NOT cover

I read this codebase top-down but didn't:

- **Run** the Web project against a real browser. The user guide
  walks every page; this report didn't.
- **Audit migrations** for downtime risk on a live DB. (231-row
  changes in `AppDbContextModelSnapshot.cs` deserve a pass.)
- **Pen-test the licensing system**. The Shamir / DPAPI / registry
  share storage is in scope for a separate review.
- **Benchmark hot paths** under load. SC-002 (< 5 s p95 at 5k docs)
  has no observed regression but no fresh number either.
- **Diff the OpenAPI contract** against the served document. The
  test that does this is one of the 11 broken — once Finding 1 is
  fixed, this becomes a real signal again.
- **Review the Inno Setup wrapper** for the bundled SQL Express
  install. Reported working on a real Windows machine but not
  re-verified here.

Each of those is a separate engagement.

---

*End of report. Read top-to-bottom; the priorities table at the
end is the only thing that needs action this week.*
