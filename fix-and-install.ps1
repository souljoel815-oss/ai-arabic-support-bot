<#
.SYNOPSIS
    One-shot: rebuild DaftarX installer with the defensive license
    gate, diagnose the previous failure, install fresh, activate,
    verify.

.DESCRIPTION
    Run this from the repo root as Administrator. The script:

    1. Captures diagnostics from the current (broken) install into
       a single log file the operator can send to support.
    2. Re-publishes the .NET Web app with the defensive LicenseGate
       fix so a licensing exception never crashes the service.
    3. Re-builds the WiX MSI + re-compiles the Inno Setup wrapper.
    4. Cleans the broken install (service, files, DB, firewall).
    5. Runs the new DaftarX-Setup.exe — the wizard pops once for
       admin email/password; the script waits.
    6. After install, polls for the service to come up. Pulls the
       Hardware ID either from the licensing banner or by invoking
       the print-hwid path.
    7. Issues a 2-year license signed with vendor-keys.json.
    8. Drops the license token into %ProgramData%\DaftarX\license\
       and restarts the service.
    9. Verifies /login serves with HTTP 200 (no banner). Reports
       success or detailed failure.

    Diagnostics land at .\daftarx-diagnostic.log so we can see what
    went wrong even after the install is wiped.
#>

$ErrorActionPreference = "Stop"
$repoRoot     = $PSScriptRoot
$diagLog      = Join-Path $repoRoot "daftarx-diagnostic.log"
$webProj      = Join-Path $repoRoot "src\EgyptTax.Web\EgyptTax.Web.csproj"
$wixProj      = Join-Path $repoRoot "src\EgyptTax.Installer\EgyptTax.Installer.wixproj"
$publishDir   = Join-Path $repoRoot "publish\EgyptTax.Web"
$msiPath      = Join-Path $repoRoot "src\EgyptTax.Installer\bin\Release\EgyptTax.Installer.msi"
$setupExe     = Join-Path $repoRoot "publish\DaftarX-Setup.exe"
$issScript    = Join-Path $repoRoot "installer-inno\daftarx-setup.iss"
$iscc         = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
$vendorKeys   = Join-Path $repoRoot "vendor-keys.json"
$licenseDir   = "C:\ProgramData\DaftarX\license"
$tempToken    = Join-Path $env:TEMP "daftarx-issue.token"
$siteUrl      = "http://localhost:8088"

function H($t) {
    "`n" + ('=' * 60) | Tee-Object -FilePath $diagLog -Append | Out-Host
    "  $t"             | Tee-Object -FilePath $diagLog -Append | Out-Host
    ('=' * 60)         | Tee-Object -FilePath $diagLog -Append | Out-Host
}
function L($msg) { $msg | Tee-Object -FilePath $diagLog -Append | Out-Host }
function CapTry($label, $sb) {
    L "--- $label ---"
    try { & $sb 2>&1 | ForEach-Object { L $_ } } catch { L "ERROR: $($_.Exception.Message)" }
}

# Reset diag log
"DaftarX install diagnostic — $(Get-Date -Format o)" | Out-File $diagLog

# ---------- Pre-flight ----------
H "0. Pre-flight"
if (-not (Test-Path $vendorKeys)) { throw "Missing vendor-keys.json at $vendorKeys — run license-keygen first." }
if (-not (Test-Path $iscc))       { throw "Inno Setup not found at $iscc." }
L "  Repo:        $repoRoot"
L "  Vendor keys: $vendorKeys"
L "  Inno Setup:  $iscc"

# ---------- 1. Diagnose CURRENT broken install ----------
H "1. Diagnostics for current install"

CapTry "Service status" { Get-Service EgyptTax -ErrorAction SilentlyContinue | Format-List Status, Name, StartType }
CapTry "Service config" { sc.exe qc EgyptTax 2>&1 }
CapTry "Port 8088"      { Get-NetTCPConnection -LocalPort 8088 -ErrorAction SilentlyContinue | Format-Table State, OwningProcess }
CapTry "Install folder" { Get-ChildItem 'C:\Program Files\EgyptTax' -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime | Format-Table }

CapTry "Service log (last 30)" {
    Get-ChildItem 'C:\Program Files\EgyptTax\logs' -ErrorAction SilentlyContinue |
      Sort-Object LastWriteTime -Desc | Select-Object -First 1 |
      ForEach-Object { Get-Content $_.FullName -Tail 30 }
}

CapTry "License gate crash log" {
    if (Test-Path 'C:\ProgramData\DaftarX\license\license-gate-crash.log') {
        Get-Content 'C:\ProgramData\DaftarX\license\license-gate-crash.log' -Tail 50
    } else { "(no crash log present)" }
}

CapTry "Application Event Log — .NET / EgyptTax errors (last 5)" {
    Get-WinEvent -LogName Application -MaxEvents 200 -ErrorAction SilentlyContinue |
      Where-Object { $_.LevelDisplayName -in 'Error','Critical' -and ($_.ProviderName -like '.NET*' -or $_.ProviderName -eq 'Application Error' -or $_.Message -match 'EgyptTax') } |
      Select-Object -First 5 |
      Format-List TimeCreated, ProviderName, Message
}

CapTry "Run EgyptTax.Web.exe manually for 6s (capture stderr)" {
    $exe = 'C:\Program Files\EgyptTax\EgyptTax.Web.exe'
    if (Test-Path $exe) {
        $job = Start-Job -ScriptBlock {
            param($p)
            & $p 2>&1
        } -ArgumentList $exe
        Start-Sleep -Seconds 6
        Stop-Job $job -ErrorAction SilentlyContinue
        Receive-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
    } else { "(exe not present)" }
}

CapTry "MSI install log (last 30 CA-related)" {
    $msi = Get-ChildItem $env:TEMP -Filter 'daftarx-msi.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Desc | Select-Object -First 1
    if ($msi) {
        "Log: $($msi.FullName)"
        Get-Content $msi.FullName | Select-String 'Return value|CustomAction|GrantSqlPermissions|ApplyMigrationsAndSeed|ApplyConfig|RegisterService|ERROR' | Select-Object -Last 30
    } else { "(no daftarx-msi.log in TEMP)" }
}

# ---------- 2. Rebuild .NET / MSI / Inno wrapper ----------
H "2. Rebuilding the installer chain (with defensive license gate fix)"

CapTry "dotnet publish" {
    Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    & dotnet publish $webProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false -o $publishDir -nologo -v quiet | Select-Object -Last 5
}

CapTry "dotnet build WiX MSI" {
    & dotnet build $wixProj -c Release -p:PublishDir="$publishDir\" -nologo -v minimal | Select-Object -Last 5
}

CapTry "ISCC compile Inno wrapper" {
    & $iscc /Q $issScript | Select-Object -Last 5
}

if (-not (Test-Path $setupExe)) { throw "Build failed — $setupExe not produced. Check the diagnostic log." }
$setupSize = "{0:N0} MB" -f ((Get-Item $setupExe).Length / 1MB)
L "  $setupExe — $setupSize"

# ---------- 3. Clean broken install ----------
H "3. Cleaning broken install"
Stop-Service EgyptTax -Force -ErrorAction SilentlyContinue
sc.exe delete EgyptTax 2>&1 | Out-Null
Remove-Item 'C:\Program Files\EgyptTax' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'C:\ProgramData\EgyptTax'   -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'C:\ProgramData\DaftarX'    -Recurse -Force -ErrorAction SilentlyContinue
sqlcmd -E -S '.\SQLEXPRESS' -Q "IF DB_ID('EgyptTax') IS NOT NULL BEGIN ALTER DATABASE EgyptTax SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE EgyptTax; END" 2>&1 | Out-Null
Get-NetFirewallRule -DisplayName "EgyptTax HTTP (8088)" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
L "  Cleaned."

# ---------- 4. Run installer (interactive — fill the wizard once) ----------
H "4. Launching installer — fill the wizard ONCE then wait for it to finish"
L "  (admin email + password are the only required fields — leave the rest as defaults)"
$proc = Start-Process -FilePath $setupExe -Wait -PassThru
L "  Installer exit code: $($proc.ExitCode)"
if ($proc.ExitCode -ne 0) {
    L "  ✗ Installer returned non-zero. Inspect $env:TEMP\daftarx-msi.log."
    throw "Installer exit $($proc.ExitCode)."
}

# ---------- 5. Wait for service to be reachable ----------
H "5. Waiting for service to start serving"
$deadline = (Get-Date).AddSeconds(120)
$bannerHtml = $null
$gotResponse = $false
while ((Get-Date) -lt $deadline) {
    try {
        $resp = Invoke-WebRequest "$siteUrl/" -UseBasicParsing -ErrorAction Stop
        $bannerHtml = $resp.Content
        $gotResponse = $true
        break
    } catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 451) {
            $bannerHtml = (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
            $gotResponse = $true
            break
        }
        Start-Sleep -Seconds 2
    }
}
if (-not $gotResponse) {
    L "  ✗ Service didn't come up within 120s."
    L "  Service: $((Get-Service EgyptTax -ErrorAction SilentlyContinue).Status)"
    L "  License crash log:"
    if (Test-Path 'C:\ProgramData\DaftarX\license\license-gate-crash.log') {
        Get-Content 'C:\ProgramData\DaftarX\license\license-gate-crash.log' | ForEach-Object { L "    $_" }
    } else { L "    (no crash log)" }
    throw "Service unreachable. See $diagLog for full diagnostics."
}
L "  Reached $siteUrl"

# ---------- 6. Extract HWID ----------
H "6. Extracting HWID from banner"
$match = [regex]::Match($bannerHtml, 'class="hwid">([^<]+)</div>')
if (-not $match.Success) {
    L "  No banner — service must have come up already activated. That shouldn't happen on a fresh install."
    L "  First 600 chars of response:"
    L $bannerHtml.Substring(0, [Math]::Min(600, $bannerHtml.Length))
    throw "HWID not found in response."
}
$hwid = $match.Groups[1].Value.Trim()
L "  HWID: $hwid"

# ---------- 7. Issue license ----------
H "7. Signing license token"
$expires = (Get-Date).AddYears(2).ToString('yyyy-MM-dd')
& dotnet run --project $webProj -c Release --no-build -- license-issue `
    --keys $vendorKeys `
    --hwid $hwid `
    --customer "Local Test Install" `
    --edition Standard `
    --expires $expires `
    --phone "+20 100 000 0000" `
    --email "sales@daftarx.local" `
    --out $tempToken | ForEach-Object { L $_ }
if ($LASTEXITCODE -ne 0) { throw "license-issue failed (exit $LASTEXITCODE)." }
if (-not (Test-Path $tempToken)) { throw "Token file not produced." }

# ---------- 8. Drop token + restart ----------
H "8. Installing license token + restarting service"
New-Item -ItemType Directory -Force $licenseDir | Out-Null
Copy-Item -Force $tempToken (Join-Path $licenseDir "license.token")
L "  Token at: $licenseDir\license.token"
Restart-Service EgyptTax -Force
Start-Sleep -Seconds 5

# ---------- 9. Verify ----------
H "9. Verifying activation"
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
    L ""
    L "==========================================================="
    L "  ✓ SUCCESS — DaftarX is installed, activated, and serving"
    L "==========================================================="
    L "  URL:     $siteUrl/login"
    L "  HWID:    $hwid"
    L "  Expires: $expires"
    L "  Diag log saved at: $diagLog"
    Write-Host "`n✓ Done — open $siteUrl/login" -ForegroundColor Green
    exit 0
} else {
    L ""
    L "==========================================================="
    L "  ✗ Activation didn't take. License crash log follows:"
    L "==========================================================="
    if (Test-Path 'C:\ProgramData\DaftarX\license\license-gate-crash.log') {
        Get-Content 'C:\ProgramData\DaftarX\license\license-gate-crash.log' | ForEach-Object { L $_ }
    }
    L ""
    L "  Last 30 service log lines:"
    Get-ChildItem 'C:\Program Files\EgyptTax\logs' -ErrorAction SilentlyContinue |
      Sort-Object LastWriteTime -Desc | Select-Object -First 1 |
      ForEach-Object { Get-Content $_.FullName -Tail 30 } |
      ForEach-Object { L "    $_" }
    Write-Host "`n✗ Failed — full diag at $diagLog" -ForegroundColor Red
    exit 1
}
