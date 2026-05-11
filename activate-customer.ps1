# Customer-side install + activation for DaftarX.
# ASCII only - safe for any transfer.
#
# Required next to this script:
#   - DaftarX-Setup.exe
#
# Usage (admin):
#   powershell -ExecutionPolicy Bypass -File .\activate-customer.ps1
param(
    [string] $SetupExe       = (Join-Path $PSScriptRoot "DaftarX-Setup.exe"),
    [string] $TokenWatchPath = $PSScriptRoot
)

$ErrorActionPreference = "Stop"
$licenseDir = "C:\ProgramData\DaftarX\license"
$siteUrl    = "http://localhost:8088"

function H($t) {
    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host ("  " + $t) -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
}
function L($m) { Write-Host ("  " + $m) }

# 0. Pre-flight
H "0. Pre-flight"
if (-not (Test-Path $SetupExe)) {
    throw ("DaftarX-Setup.exe not found at " + $SetupExe + ". Pass -SetupExe path or put it next to this script.")
}
$mb = [math]::Round((Get-Item $SetupExe).Length / 1MB, 1)
L ("Setup       : " + $SetupExe + " (" + $mb + " MB)")
L ("Token watch : " + $TokenWatchPath)

# 1. Clean
H "1. Cleaning any prior DaftarX install"
Stop-Service EgyptTax -Force -ErrorAction SilentlyContinue
sc.exe delete EgyptTax 2>&1 | Out-Null
Remove-Item 'C:\Program Files\EgyptTax' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'C:\ProgramData\EgyptTax'   -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'C:\ProgramData\DaftarX'    -Recurse -Force -ErrorAction SilentlyContinue
sqlcmd -E -S '.\SQLEXPRESS' -Q "IF DB_ID('EgyptTax') IS NOT NULL BEGIN ALTER DATABASE EgyptTax SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE EgyptTax; END" 2>&1 | Out-Null
Get-NetFirewallRule -DisplayName "EgyptTax HTTP (8088)" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
L "Cleaned."

# 2. Run installer
H "2. Running installer - fill the wizard ONCE then wait"
L "(admin email + password are the only required fields)"
$proc = Start-Process -FilePath $SetupExe -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    throw ("Installer exit " + $proc.ExitCode + ". Inspect %TEMP%\daftarx-msi.log.")
}
L "Installer finished."

# 3. Wait for service / banner
H "3. Waiting for the licensing banner"
$deadline = (Get-Date).AddSeconds(180)
$bannerHtml = $null
while ((Get-Date) -lt $deadline) {
    try {
        $resp = Invoke-WebRequest "$siteUrl/" -UseBasicParsing -ErrorAction Stop
        $bannerHtml = $resp.Content
        break
    } catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 451) {
            $bannerHtml = (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
            break
        }
        Start-Sleep -Seconds 2
    }
}
if (-not $bannerHtml) {
    throw "Service did not come up within 180s. Run Diagnose-DaftarX.cmd to capture the failure."
}

# 4. Extract HWID
H "4. Hardware ID for activation"
$match = [regex]::Match($bannerHtml, 'class="hwid">([^<]+)</div>')
if (-not $match.Success) {
    throw ("Could not parse HWID from banner. Snippet: " + $bannerHtml.Substring(0, [Math]::Min(400, $bannerHtml.Length)))
}
$hwid = $match.Groups[1].Value.Trim()

Write-Host ""
Write-Host "  ====================================================" -ForegroundColor Yellow
Write-Host "    HWID for this machine:" -ForegroundColor Yellow
Write-Host ("    " + $hwid) -ForegroundColor Yellow
Write-Host "  ====================================================" -ForegroundColor Yellow
Write-Host ""
L "Send this HWID to the vendor. They run on their dev machine:"
Write-Host ""
Write-Host "  dotnet run --project src/EgyptTax.Web/EgyptTax.Web.csproj -c Release --no-build -- ``" -ForegroundColor Gray
Write-Host "    license-issue --keys vendor-keys.json ``" -ForegroundColor Gray
Write-Host ("                  --hwid " + $hwid + " ``") -ForegroundColor Gray
Write-Host "                  --customer ""<customer name>"" ``" -ForegroundColor Gray
Write-Host "                  --expires 2027-12-31 ``" -ForegroundColor Gray
Write-Host "                  --out license.token" -ForegroundColor Gray
Write-Host ""
L "Vendor sends back a file: license.token"
L ("Drop it here:  " + (Join-Path $TokenWatchPath "license.token"))
Write-Host ""

# 5. Watch for token
H "5. Waiting for license.token"
$tokenWatchFile = Join-Path $TokenWatchPath "license.token"
$lastSize = -1
$spinner = @('|','/','-','\')
$si = 0
while (-not (Test-Path $tokenWatchFile)) {
    Write-Host -NoNewline ("`r  Watching " + $TokenWatchPath + "  " + $spinner[$si % 4] + "  (Ctrl+C to abort)")
    Start-Sleep -Milliseconds 600
    $si++
}
do {
    Start-Sleep -Milliseconds 500
    $size = (Get-Item $tokenWatchFile).Length
    $stable = ($size -eq $lastSize)
    $lastSize = $size
} while (-not $stable)
Write-Host ""
L ("Found token: " + $tokenWatchFile + " (" + $lastSize + " bytes)")

# 6. Install token + restart
H "6. Installing license token + restarting service"
New-Item -ItemType Directory -Force $licenseDir | Out-Null
Copy-Item -Force $tokenWatchFile (Join-Path $licenseDir "license.token")
L ("Copied to " + $licenseDir + "\license.token")
Restart-Service EgyptTax -Force
Start-Sleep -Seconds 5

# 7. Verify
H "7. Verifying activation"
$ok = $false
for ($i = 0; $i -lt 12; $i++) {
    try {
        $r = Invoke-WebRequest "$siteUrl/login" -UseBasicParsing -ErrorAction Stop
        if ($r.StatusCode -eq 200 -and $r.Content -notmatch 'class="hwid"') {
            $ok = $true
            break
        }
    } catch { }
    Start-Sleep -Seconds 2
}

if ($ok) {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "  SUCCESS - DaftarX activated and serving" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host ("  URL  : " + $siteUrl + "/login") -ForegroundColor Green
    Write-Host ("  HWID : " + $hwid) -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host "  FAILED - Activation did not take." -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Red
    L "Possible causes:"
    L "  - HWID in license.token does not match this machine."
    L "  - License signed with the wrong vendor-keys.json."
    L "  - License already expired."
    L ""
    L "License gate crash log (if any):"
    if (Test-Path 'C:\ProgramData\DaftarX\license\license-gate-crash.log') {
        Get-Content 'C:\ProgramData\DaftarX\license\license-gate-crash.log' | ForEach-Object { L $_ }
    } else { L "  (no crash log)" }
    exit 1
}
