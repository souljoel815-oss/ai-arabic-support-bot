<#
.SYNOPSIS
    End-to-end DaftarX install on this machine.

.DESCRIPTION
    1. Stops + removes any prior install (service, files, DB, firewall).
    2. Runs DaftarX-Setup.exe — you fill the wizard ONCE (email/password).
    3. After install, polls localhost:8088 until the licensing banner appears.
    4. Extracts the Hardware ID from the banner.
    5. Issues a license signed with vendor-keys.json (matching the public
       key embedded in this build).
    6. Drops the license token into the activation directory.
    7. Restarts the EgyptTax service.
    8. Verifies /login returns 200 and the app is reachable.

    Run from the repo root as Administrator.
#>

$ErrorActionPreference = "Stop"
$repoRoot     = $PSScriptRoot
$setupExe     = Join-Path $repoRoot "publish\DaftarX-Setup.exe"
$vendorKeys   = Join-Path $repoRoot "vendor-keys.json"
$webProj      = Join-Path $repoRoot "src\EgyptTax.Web\EgyptTax.Web.csproj"
$licenseDir   = "C:\ProgramData\DaftarX\license"
$licenseToken = Join-Path $licenseDir "license.token"
$genTokenOut  = Join-Path $env:TEMP "daftarx-issue.token"
$siteUrl      = "http://localhost:8088"

function Step($n, $msg) { Write-Host "`n[$n] $msg" -ForegroundColor Cyan }

# ---------- Pre-flight ----------
Step "0" "Pre-flight checks"
if (-not (Test-Path $setupExe))   { throw "Missing $setupExe — run the build pipeline first." }
if (-not (Test-Path $vendorKeys)) { throw "Missing $vendorKeys — run license-keygen and place the file at the repo root." }
if (-not (Test-Path $webProj))    { throw "Missing $webProj — run from repo root." }
"  Setup     : $setupExe"
"  Keys      : $vendorKeys"
"  Web proj  : $webProj"

# ---------- 1. Clean ----------
Step "1" "Cleaning prior install"
Stop-Service EgyptTax -Force -ErrorAction SilentlyContinue
sc.exe delete EgyptTax 2>&1 | Out-Null
Remove-Item 'C:\Program Files\EgyptTax' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'C:\ProgramData\EgyptTax'   -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'C:\ProgramData\DaftarX'    -Recurse -Force -ErrorAction SilentlyContinue
sqlcmd -E -S '.\SQLEXPRESS' -Q "IF DB_ID('EgyptTax') IS NOT NULL BEGIN ALTER DATABASE EgyptTax SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE EgyptTax; END" 2>&1 | Out-Null
Get-NetFirewallRule -DisplayName "EgyptTax HTTP (8088)" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
"  Cleaned."

# ---------- 2. Run installer ----------
Step "2" "Launching DaftarX-Setup.exe — fill the wizard ONCE then wait for it to finish"
"  (admin email + password are the only required fields; everything else has defaults)"
$proc = Start-Process -FilePath $setupExe -Wait -PassThru
if ($proc.ExitCode -ne 0) { throw "DaftarX-Setup.exe exited $($proc.ExitCode)" }
"  Installer finished."

# ---------- 3. Wait for the banner ----------
Step "3" "Waiting for the EgyptTax service + banner page"
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
if (-not $bannerHtml) { throw "Banner did not appear within 180s. Inspect Get-Service EgyptTax + the MSI log in `$env:TEMP\daftarx-msi.log." }
"  Banner reached."

# ---------- 4. Extract HWID ----------
Step "4" "Extracting Hardware ID from banner"
$match = [regex]::Match($bannerHtml, 'class="hwid">([^<]+)</div>')
if (-not $match.Success) { throw "Could not parse HWID from banner. Banner snippet: $($bannerHtml.Substring(0, [Math]::Min(400, $bannerHtml.Length)))" }
$hwid = $match.Groups[1].Value.Trim()
"  HWID: $hwid"

# ---------- 5. Issue a license ----------
Step "5" "Signing a license token for this HWID"
$expires = (Get-Date).AddYears(2).ToString('yyyy-MM-dd')
& dotnet run --project $webProj -c Release --no-build -- license-issue `
    --keys $vendorKeys `
    --hwid $hwid `
    --customer "Local Test Install" `
    --edition Standard `
    --expires $expires `
    --phone "+20 100 000 0000" `
    --email "sales@daftarx.local" `
    --out $genTokenOut
if ($LASTEXITCODE -ne 0) { throw "license-issue failed (exit $LASTEXITCODE)." }
"  Token written to $genTokenOut"

# ---------- 6. Drop the token ----------
Step "6" "Installing license token into the activation directory"
New-Item -ItemType Directory -Force $licenseDir | Out-Null
Copy-Item -Force $genTokenOut $licenseToken
"  $licenseToken in place."

# ---------- 7. Restart the service ----------
Step "7" "Restarting EgyptTax service to pick up the activation"
Restart-Service EgyptTax -Force
Start-Sleep -Seconds 3

# ---------- 8. Verify ----------
Step "8" "Verifying the app serves cleanly"
$attempts = 0
$ok = $false
while ($attempts -lt 10) {
    try {
        $r = Invoke-WebRequest "$siteUrl/login" -UseBasicParsing -ErrorAction Stop
        if ($r.StatusCode -eq 200 -and $r.Content -notmatch 'class="hwid"') {
            $ok = $true
            break
        }
    } catch {
        # 451 means the activation didn't take — fall through to error path
    }
    Start-Sleep -Seconds 2
    $attempts++
}

if ($ok) {
    Write-Host "`n========================================================" -ForegroundColor Green
    Write-Host "  ✓ DaftarX is installed + activated + serving" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
    "  URL    : $siteUrl/login"
    "  HWID   : $hwid"
    "  Expires: $expires"
} else {
    Write-Host "`n========================================================" -ForegroundColor Red
    Write-Host "  ✗ Activation didn't take — license token may have been rejected" -ForegroundColor Red
    Write-Host "========================================================" -ForegroundColor Red
    "Diagnostics:"
    "  - HWID parsed from banner : $hwid"
    "  - Token at                : $licenseToken"
    "  - Service status          : $((Get-Service EgyptTax).Status)"
    "  - Last 10 lines of service log:"
    Get-ChildItem 'C:\Program Files\EgyptTax\logs' -ErrorAction SilentlyContinue |
        Sort LastWriteTime -Desc | Select -First 1 | ForEach-Object { Get-Content $_.FullName -Tail 10 }
    exit 1
}
