# EgyptTax MSI Installer (T247)

WiX 5 source for the on-prem MSI installer. Built via `wix build` on Windows; the resulting `EgyptTax-Setup-{version}.msi` is what operators run to install the EgyptTax web service on a Windows host.

## What this directory contains

| File | Purpose |
|---|---|
| `Product.wxs` | Main WiX source — package metadata, install directory, Windows service registration, firewall rule, custom actions for seed + health gate. |
| `appsettings.template.json` | Application config skeleton; the MSI ships this as the per-install config seed. Operator-supplied SQL connection / audit-checkpoint mode / attachments root / NTP server land here at install time (currently via msiexec property bootstrap; full UI dialog page is Near-term). |
| `verify-health.ps1` | T257 health-readiness gate. Polls `/api/v1/health/ready` after the service starts; non-Healthy fails the install (rollback) so the "install complete" screen never fires while the service is still booting. |

## Build (CI)

The `build-installer` job in [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) executes:

```powershell
dotnet tool install --global wix --version 5.0.2
dotnet publish src/EgyptTax.Web/EgyptTax.Web.csproj -c Release -r win-x64 --self-contained false -o publish/
wix build src/EgyptTax.Installer/Product.wxs -o EgyptTax-Setup.msi -arch x64
```

The job is gated by `if: github.ref == 'refs/heads/main' || startsWith(github.ref, 'refs/tags/v')` so feature branches don't burn CI time on installer builds.

## Build (local / dev)

```powershell
# One-time: install WiX 5
dotnet tool install --global wix --version 5.0.2

# Publish the web app to ./publish/
dotnet publish src\EgyptTax.Web\EgyptTax.Web.csproj -c Release -r win-x64 --self-contained false -o .\publish\

# Harvest the publish output into a Components fragment (Near-term:
# wire this into the wixproj so it happens automatically; current
# path is a manual `wix harvest` invocation):
# wix extension add WixToolset.Util.wixext
# (full harvest invocation TBD — see "Open work" below)

# Build the MSI
wix build src\EgyptTax.Installer\Product.wxs -o EgyptTax-Setup.msi -arch x64
```

## Operator-prompted properties

Set on the `msiexec` command line until the WiX UI dialog page lands:

```powershell
msiexec /i EgyptTax-Setup.msi `
    SQL_CONNECTION="Server=.;Database=EgyptTax;Trusted_Connection=True;TrustServerCertificate=True" `
    AUDIT_CHECKPOINT_MODE=Sql `
    ATTACHMENTS_ROOT="D:\EgyptTax\attachments" `
    NTP_SERVER=time.windows.com
```

| Property | Default | Purpose |
|---|---|---|
| `SQL_CONNECTION` | local default instance with Trusted_Connection | EF Core's connection to the EgyptTax database. |
| `AUDIT_CHECKPOINT_MODE` | `Sql` | `Sql` (default — separate SQL schema) or `FileSystem` (file outside SQL's reach for stronger sysadmin separation per FR-028). |
| `ATTACHMENTS_ROOT` | `%ProgramData%\EgyptTax\attachments` | Receipt upload root (FR-019). |
| `NTP_SERVER` | `time.windows.com` | Clock-drift defense (FR-042). |

See [`docs/operator-runbook.md`](../../docs/operator-runbook.md) §1 for the full install flow and §1.3 for the audit-checkpoint mode trade-off.

## Install-time custom actions (sequence)

1. **`ApplyMigrationsAndSeed`** (after `InstallFiles`, before service start) — runs `EgyptTax.Web.exe seed --apply-migrations` to create the schema + seed the first Administrator. Non-zero exit rolls the install back.
2. **`VerifyHealthReady`** (T257, after `StartServices`) — runs `verify-health.ps1` to poll the readiness probe up to 10 times. Non-Healthy rolls the install back. **Without this gate the operator's "install complete" screen could fire while the service is still booting + immediately broken.**

Both actions are conditional on `NOT REMOVE` so they don't fire on uninstall.

## Open work / deferred (not blocking the basic install path)

- **WiX UI dialog pages for the 4 prompted properties.** The MVP relies on `msiexec` command-line property bootstrap; the full WixUI_Mondo dialog set + a custom property page belongs to a Near-term batch. The upgrade is purely cosmetic — the install otherwise produces a working service today via the command-line path.
- **Component harvesting wired into the wixproj.** The `wix harvest` invocation that turns the `publish/` output into a `Components.wxs` fragment is currently a manual step; making it automatic via `Target` integration in the wixproj is Near-term. Until then, CI's `wix build` step expects a pre-harvested fragment OR uses the `IncludeFile` attribute on the Package (TBD per CI feedback).
- **Service-account dialog** for installs that need a dedicated domain account instead of `NT AUTHORITY\NetworkService`. Default works for most installs (Trusted_Connection against local default SQL); the dialog page lands when the first cross-domain customer signal comes in.
- **Per-environment SQL bootstrap.** The `seed --apply-migrations` CLI uses EF Core's `Database.MigrateAsync()` which creates the database if it doesn't exist. Some IT shops want a separate "create the DB with these size + collation settings, THEN run migrations" step — that's a future MSI dialog page.

## Uninstall behavior

- Files installed to `%ProgramFiles%\EgyptTax\` are removed.
- The Windows service is stopped + deregistered.
- The HTTPS 443 firewall rule is removed.
- **Application data** (the SQL database, the attachments directory under `%ProgramData%\EgyptTax\`, the audit-checkpoint files) is **preserved** — uninstall is non-destructive of taxpayer data per FR-027 (immutable history). Operators who genuinely want to wipe the install must do so manually after uninstall.
