<#
.SYNOPSIS
    Generate a signed DaftarX license.token for a specific customer
    HWID. Wraps the EgyptTax.Web license-issue CLI so the operator
    doesn't have to remember the long flag list or fight with paths
    (dotnet run shifts cwd, so vendor-keys.json must be absolute).

.DESCRIPTION
    Reads HWID + customer + expiry (interactive prompt or
    parameters), invokes the signing CLI, and stores the result in
    licenses\<HWID>\license.token next to a small README pointing
    the customer at the install path.

    Re-running for the same HWID overwrites the existing token —
    that's the intended path when a customer's license is renewed
    or re-issued.

    Run from the repo root. Requires:
      * dotnet 8 SDK on PATH
      * vendor-keys.json present at repo root (gitignored)
      * src\EgyptTax.Web project builds

.PARAMETER Hwid
    Hardware ID printed by the customer's installer
    (XXXX-XXXX-XXXX-XXXX). If omitted, prompts.

.PARAMETER Customer
    Customer display name (Arabic or English). Shown on the
    activation banner inside the app. If omitted, prompts.

.PARAMETER Expires
    Expiry date (yyyy-MM-dd). Default: 1 year from today.

.PARAMETER Edition
    Solo | SMB | Enterprise | Firm (Gux.13 4-edition scheme).
    Default: SMB. Pre-Gux.13 values (Standard / Pro / Basic) are
    accepted for backward compatibility but mapped to Solo at
    activation time.

.PARAMETER MaxUsers
    Override the default user cap for the chosen edition. Defaults:
    Solo=1, SMB=3, Enterprise=unlimited, Firm=unlimited.

.PARAMETER MaxCompanies
    Override the default company cap. Defaults: Solo=1, SMB=1,
    Enterprise=3, Firm=unlimited.

.PARAMETER ExtraFeatures
    Comma-separated extra feature flags to add on top of the
    edition's default Features[] (e.g., promotional bundle giving
    an SMB customer one Enterprise feature). Use the constants
    from src\EgyptTax.Web\Licensing\Feature.cs.

.PARAMETER Phone
    Sales contact phone shown on the activation banner.
    Default: +20 100 000 0000.

.PARAMETER Email
    Sales contact email shown on the activation banner.
    Default: sales@daftarx.local.

.PARAMETER OutDir
    Where to drop the per-customer folder. Default:
    .\licenses\<HWID>\

.EXAMPLE
    .\Issue-License.ps1
    # Prompts for everything.

.EXAMPLE
    .\Issue-License.ps1 -Hwid 017F-0D1A-1BA1-C968 -Customer "Hope Co" -Expires 2027-12-31

.EXAMPLE
    .\Issue-License.ps1 -Hwid ABCD-1234-EF56-7890 -Customer "Light Trading" -Edition Pro
#>
[CmdletBinding()]
param(
    [string] $Hwid,
    [string] $Customer,
    [string] $Expires,
    # Gux.13 4-edition scheme. Pre-Gux.13 values (Standard, Pro,
    # Basic) still accepted for back-compat scripts; they map to
    # Solo at activation time.
    [ValidateSet('Solo', 'SMB', 'Enterprise', 'Firm', 'Standard', 'Pro', 'Basic')]
    [string] $Edition = 'SMB',
    [int]    $MaxUsers,
    [int]    $MaxCompanies,
    [string] $ExtraFeatures,
    [string] $Phone   = '+20 100 000 0000',
    [string] $Email   = 'sales@daftarx.local',
    [string] $OutDir
)

$ErrorActionPreference = 'Stop'
$RepoRoot = $PSScriptRoot
Set-Location $RepoRoot

# --- Sanity checks ----------------------------------------------------
$keysPath = Join-Path $RepoRoot 'vendor-keys.json'
if (-not (Test-Path $keysPath)) {
    Write-Error @"
vendor-keys.json not found at:
  $keysPath

This is the private signing key — without it, no license can be
issued. If you've lost it, you must rotate the public key in
src\EgyptTax.Web\Licensing\LicensePublicKey.cs and rebuild every
customer install.
"@
    exit 2
}

$webProject = Join-Path $RepoRoot 'src\EgyptTax.Web\EgyptTax.Web.csproj'
if (-not (Test-Path $webProject)) {
    Write-Error "EgyptTax.Web project not found at: $webProject"
    exit 2
}

# --- Interactive prompts when args missing ----------------------------
if ([string]::IsNullOrWhiteSpace($Hwid)) {
    $Hwid = Read-Host "Customer HWID (XXXX-XXXX-XXXX-XXXX)"
}
$Hwid = $Hwid.Trim().ToUpperInvariant()
if ($Hwid -notmatch '^[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$') {
    Write-Error "HWID must look like XXXX-XXXX-XXXX-XXXX (hex). Got: $Hwid"
    exit 3
}

if ([string]::IsNullOrWhiteSpace($Customer)) {
    $Customer = Read-Host "Customer display name"
}
if ([string]::IsNullOrWhiteSpace($Customer)) {
    Write-Error "Customer name is required."
    exit 3
}

if ([string]::IsNullOrWhiteSpace($Expires)) {
    $defaultExpiry = (Get-Date).AddYears(1).ToString('yyyy-MM-dd')
    $entered = Read-Host "Expires (yyyy-MM-dd) [default: $defaultExpiry]"
    if ([string]::IsNullOrWhiteSpace($entered)) { $Expires = $defaultExpiry }
    else { $Expires = $entered.Trim() }
}
$null = [datetime]::ParseExact($Expires, 'yyyy-MM-dd', $null)

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path (Join-Path $RepoRoot 'licenses') $Hwid
}
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}
$tokenPath = Join-Path $OutDir 'license.token'

# --- Sign ------------------------------------------------------------
# Resolve effective MaxUsers / MaxCompanies / Features from the
# edition's defaults if the caller didn't override. The signing CLI
# accepts these as explicit args; the runtime gate also has the
# same defaults baked in (Feature.DefaultsFor) so omitting the
# Features list still produces a license that gates correctly.
$effectiveMaxUsers     = if ($PSBoundParameters.ContainsKey('MaxUsers'))     { $MaxUsers     } else { 0 }
$effectiveMaxCompanies = if ($PSBoundParameters.ContainsKey('MaxCompanies')) { $MaxCompanies } else { 0 }
$effectiveFeatures     = if ([string]::IsNullOrWhiteSpace($ExtraFeatures))   { ''            } else { $ExtraFeatures.Trim() }

Write-Host ""
Write-Host "Signing license:"
Write-Host "  HWID:         $Hwid"
Write-Host "  Customer:     $Customer"
Write-Host "  Edition:      $Edition"
Write-Host "  Expires:      $Expires"
if ($effectiveMaxUsers     -gt 0) { Write-Host "  MaxUsers:     $effectiveMaxUsers (override)" }
if ($effectiveMaxCompanies -gt 0) { Write-Host "  MaxCompanies: $effectiveMaxCompanies (override)" }
if ($effectiveFeatures)           { Write-Host "  +Features:    $effectiveFeatures" }
Write-Host "  Output:       $tokenPath"
Write-Host ""

# dotnet run -- args; absolute paths because dotnet run shifts cwd
# to the project directory before invoking the CLI.
$cliArgs = @(
    'license-issue',
    '--keys', $keysPath,
    '--hwid', $Hwid,
    '--customer', $Customer,
    '--expires', $Expires,
    '--edition', $Edition,
    '--phone', $Phone,
    '--email', $Email,
    '--out', $tokenPath
)
if ($effectiveMaxUsers     -gt 0) { $cliArgs += @('--max-users',     $effectiveMaxUsers) }
if ($effectiveMaxCompanies -gt 0) { $cliArgs += @('--max-companies', $effectiveMaxCompanies) }
if ($effectiveFeatures)           { $cliArgs += @('--features',      $effectiveFeatures) }

& dotnet run --project $webProject --no-launch-profile -- @cliArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "license-issue exited $LASTEXITCODE"
    exit $LASTEXITCODE
}

# --- Drop a customer-facing README so support doesn't get the
# "where do I put this file?" question every time. -------------------
$readme = @"
DaftarX License - $Customer
HWID: $Hwid
Edition: $Edition
Expires: $Expires
Issued: $(Get-Date -Format 'yyyy-MM-dd HH:mm') UTC

INSTALL:
  1. Copy license.token to:
       %PROGRAMDATA%\DaftarX\license\license.token
     (full path: C:\ProgramData\DaftarX\license\license.token)
  2. Restart the EgyptTax service:
       net stop  EgyptTax
       net start EgyptTax
  3. Open https://localhost:8088 - the activation banner should
     disappear.

If activation fails, run Diagnose-DaftarX.cmd from the install
folder and send daftarx-diagnostic.txt back to support.
"@
Set-Content -Path (Join-Path $OutDir 'README.txt') -Value $readme -Encoding UTF8

Write-Host ""
Write-Host "Done. Send these two files to the customer:"
Write-Host "  $tokenPath"
Write-Host "  $(Join-Path $OutDir 'README.txt')"
exit 0
