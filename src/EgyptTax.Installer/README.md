# EgyptTax MSI Installer (T247 + T247-followup)

WiX 5 source for the on-prem MSI installer. The wixproj wraps the WiX SDK so building the installer is a standard `dotnet build` invocation; the resulting `EgyptTax.Installer.msi` (rename to `EgyptTax-Setup-{version}.msi` for distribution) is what operators run to install the EgyptTax web service on a Windows host.

## What this directory contains

| File | Purpose |
|---|---|
| `EgyptTax.Installer.wixproj` | MSBuild project file using `WixToolset.Sdk/5.0.2`. Pulls in `WixToolset.Firewall.wixext` via `PackageReference`. Defines the WiX preprocessor variable `PublishDir` so `Product.wxs`'s `<Files>` element can harvest the published web-app output. |
| `Product.wxs` | Main WiX source — package metadata, install directory, `<Files>` auto-harvest of the publish output, Windows service registration, firewall rule, custom actions for seed + health gate. |
| `verify-health.ps1` | T257 health-readiness gate. Polls `/api/v1/health/ready` after the service starts; non-Healthy fails the install (rollback) so the "install complete" screen never fires while the service is still booting. |

## Build (CI)

The `build-installer` job in [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) executes:

```powershell
dotnet publish src/EgyptTax.Web/EgyptTax.Web.csproj -c Release -r win-x64 --self-contained false -o ${{ github.workspace }}/publish
dotnet build src/EgyptTax.Installer/EgyptTax.Installer.wixproj -c Release -p:HarvestPublishDir=${{ github.workspace }}/publish
```

The job is gated by `if: github.ref == 'refs/heads/main' || startsWith(github.ref, 'refs/tags/v')` so feature branches don't burn CI time on installer builds. **The previous `continue-on-error: true` flag has been removed** — the harvest-into-wixproj wiring is in place + verified locally (17 MB MSI produced cleanly with `0 Warning(s) 0 Error(s)`).

## Build (local / dev)

```powershell
# One-time: nothing! The wixproj's <PackageReference Include="WixToolset.Firewall.wixext" />
# pulls the SDK + extension automatically on first build.

# Step 1: publish the web app to ./publish/
dotnet publish src\EgyptTax.Web\EgyptTax.Web.csproj -c Release -r win-x64 --self-contained false -o .\publish\

# Step 2: build the MSI
dotnet build src\EgyptTax.Installer\EgyptTax.Installer.wixproj -c Release -p:HarvestPublishDir=$(pwd)\publish

# Output:
#   src\EgyptTax.Installer\bin\Release\EgyptTax.Installer.msi
```

**Note**: the property is `HarvestPublishDir`, NOT MSBuild's default `PublishDir` — that name is reserved by MSBuild for the SDK's own publish-output directory and using it here causes a property collision. See the wixproj's PropertyGroup for details.

The wixproj is intentionally NOT included in `EgyptTax.sln` — pulling the WiX SDK into every solution-build slows the dev loop without payoff (the installer rarely needs to rebuild during day-to-day code changes).

### Direct `wix build` invocation (alternative)

If you'd rather skip the wixproj and call `wix.exe` directly:

```powershell
dotnet tool install --global wix --version 5.0.2
wix extension add WixToolset.Firewall.wixext -g
wix build src\EgyptTax.Installer\Product.wxs `
    -ext WixToolset.Firewall.wixext `
    -o EgyptTax-Setup.msi -arch x64 `
    -d PublishDir=publish
```

This produces the same MSI; the wixproj path is preferred because it's idiomatic for the rest of the .NET toolchain.

## How the `<Files>` harvest works

WiX 5's `<Files>` element auto-generates one Component per file at build time with a stable Guid-from-path so MajorUpgrade scenarios work without manual ComponentGuid bookkeeping:

```xml
<ComponentGroup Id="EgyptTaxFiles" Directory="INSTALLFOLDER">
  <Files Include="$(var.PublishDir)\**" />
</ComponentGroup>
```

The `<Files>` element does NOT support an `Exclude` attribute (WiX 5 schema), so a file claimed by a separate Component (e.g. as a `ServiceInstall` keypath) would cause a duplicate-component conflict. The installer sidesteps this by registering the Windows service via `sc.exe` in custom actions (`CreateService` / `StartService` / `StopService` / `DeleteService`) rather than a WiX `ServiceInstall` element — `EgyptTax.Web.exe` is harvested alongside the rest of the publish output, and the service registration points at `[INSTALLFOLDER]EgyptTax.Web.exe` after install.

Trade-off: `sc.exe`-based service registration produces an installer that's slightly less idiomatic than one using ServiceInstall, but operationally equivalent (auto-start service, NetworkService account, stops + deletes on uninstall via the symmetric custom actions). The win is a much simpler harvest with no per-extension globbing.

### Required wixproj knobs

The wixproj sets a few properties that aren't optional:

- `EnableDefaultItems=false` (and the four siblings) — without this, the WiX SDK auto-includes content under `HarvestPublishDir` a second time and trips `WIX8602` "already harvested" errors on every file.
- `SuppressIces=ICE60` — silences the validator for .NET satellite-resource DLLs (`en/EgyptTax.Web.resources.dll`, etc) which carry version info but no Language metadata. .NET resolves localized resources via culture-named subdirectories, not the MSI Language column, so the suppression is operationally safe.

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
- **WixUtil's XmlConfig support to write the operator-supplied properties into the published `appsettings.json` at install time.** Today: operator hand-edits per docs/operator-runbook.md §1.2.
- **Service-account dialog** for installs that need a dedicated domain account instead of `NT AUTHORITY\NetworkService`. Default works for most installs (Trusted_Connection against local default SQL); the dialog page lands when the first cross-domain customer signal comes in.
- **Per-environment SQL bootstrap.** The `seed --apply-migrations` CLI uses EF Core's `Database.MigrateAsync()` which creates the database if it doesn't exist. Some IT shops want a separate "create the DB with these size + collation settings, THEN run migrations" step — that's a future MSI dialog page.

## Uninstall behavior

- Files installed to `%ProgramFiles%\EgyptTax\` are removed.
- The Windows service is stopped + deregistered.
- The HTTPS 443 firewall rule is removed.
- **Application data** (the SQL database, the attachments directory under `%ProgramData%\EgyptTax\`, the audit-checkpoint files) is **preserved** — uninstall is non-destructive of taxpayer data per FR-027 (immutable history). Operators who genuinely want to wipe the install must do so manually after uninstall.
