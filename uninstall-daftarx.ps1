# DaftarX uninstaller - full wipe.
# Standard mode: removes DaftarX, keeps SQL Server Express.
# -Nuclear     : also flag SQL Express for manual uninstall.
# ASCII only - safe for any transfer / encoding.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\uninstall-daftarx.ps1
#   powershell -ExecutionPolicy Bypass -File .\uninstall-daftarx.ps1 -Nuclear
param(
    [switch] $Nuclear
)

$ErrorActionPreference = "Continue"

function Step($n, $msg) { Write-Host ("`n[" + $n + "] " + $msg) -ForegroundColor Cyan }
function OK($msg)       { Write-Host ("    OK   " + $msg) -ForegroundColor Green }
function Skip($msg)     { Write-Host ("    .    " + $msg) -ForegroundColor DarkGray }
function Warn($msg)     { Write-Host ("    WARN " + $msg) -ForegroundColor Yellow }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Must run as Administrator." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  DaftarX uninstall - full wipe" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
if ($Nuclear) {
    Write-Host "  Nuclear mode requested." -ForegroundColor Yellow
}

# 1. Service
Step "1" "EgyptTax Windows service"
$svc = Get-Service EgyptTax -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -eq 'Running') {
        Stop-Service EgyptTax -Force -ErrorAction SilentlyContinue
        OK "Stopped service"
    } else {
        Skip ("Service already " + $svc.Status)
    }
    sc.exe delete EgyptTax 2>&1 | Out-Null
    Start-Sleep -Seconds 2
    if (Get-Service EgyptTax -ErrorAction SilentlyContinue) {
        Warn "Service still present (marked-for-delete; clears after reboot)"
    } else {
        OK "Deleted service registration"
    }
} else {
    Skip "Service not installed"
}

# 2. Filesystem
Step "2" "Filesystem cleanup"
$paths = @(
    'C:\Program Files\EgyptTax',
    'C:\ProgramData\EgyptTax',
    'C:\ProgramData\DaftarX',
    (Join-Path $env:APPDATA 'DaftarX'),
    'C:\Users\Public\Desktop\DaftarX.url',
    'C:\Users\Public\Desktop\DaftarX.lnk',
    'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\DaftarX'
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        try {
            Remove-Item $p -Recurse -Force -ErrorAction Stop
            OK ("Removed " + $p)
        } catch {
            Warn ("Could not remove " + $p + " - " + $_.Exception.Message)
        }
    } else {
        Skip ("Not present: " + $p)
    }
}

# Per-user shortcuts on every user
foreach ($userDir in (Get-ChildItem 'C:\Users' -Directory -ErrorAction SilentlyContinue)) {
    $candidates = @(
        (Join-Path $userDir.FullName 'Desktop\DaftarX.url'),
        (Join-Path $userDir.FullName 'Desktop\DaftarX.lnk'),
        (Join-Path $userDir.FullName 'AppData\Roaming\DaftarX'),
        (Join-Path $userDir.FullName 'AppData\Roaming\Microsoft\Windows\Start Menu\Programs\DaftarX')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            try { Remove-Item $c -Recurse -Force -ErrorAction Stop; OK ("Removed " + $c) }
            catch { Warn ("Could not remove " + $c) }
        }
    }
}

# 3. Registry
Step "3" "Registry cleanup"
$licenseKey = 'HKCU:\Software\DaftarX'
if (Test-Path $licenseKey) {
    Remove-Item $licenseKey -Recurse -Force -ErrorAction SilentlyContinue
    OK ("Removed " + $licenseKey)
} else {
    Skip ("Not present: " + $licenseKey)
}

foreach ($k in @('HKLM:\Software\EgyptTax', 'HKLM:\Software\DaftarX')) {
    if (Test-Path $k) { Remove-Item $k -Recurse -Force -ErrorAction SilentlyContinue; OK ("Removed " + $k) }
}

$uninstallRoots = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
foreach ($root in $uninstallRoots) {
    if (-not (Test-Path $root)) { continue }
    Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $disp = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).DisplayName
            if ($disp -match 'DaftarX' -or $disp -match 'EgyptTax') {
                Remove-Item $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue
                OK ("Removed uninstall entry: " + $disp)
            }
        } catch { }
    }
}

# Per-user uninstall stubs (Inno per-user installs)
foreach ($userDir in (Get-ChildItem 'C:\Users' -Directory -ErrorAction SilentlyContinue)) {
    $hive = Join-Path $userDir.FullName 'NTUSER.DAT'
    if (-not (Test-Path $hive)) { continue }
    $tempHive = "DftxUninst_" + $userDir.Name
    reg.exe load ("HKU\" + $tempHive) $hive 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $userInnoKey = "Registry::HKEY_USERS\" + $tempHive + "\Software\Microsoft\Windows\CurrentVersion\Uninstall"
        if (Test-Path $userInnoKey) {
            Get-ChildItem $userInnoKey -ErrorAction SilentlyContinue | ForEach-Object {
                $disp = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).DisplayName
                if ($disp -match 'DaftarX|EgyptTax') {
                    Remove-Item $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue
                    OK ("Removed per-user uninstall entry for " + $userDir.Name + ": " + $disp)
                }
            }
        }
        [GC]::Collect(); Start-Sleep -Milliseconds 200
        reg.exe unload ("HKU\" + $tempHive) 2>&1 | Out-Null
    }
}

# 4. Firewall
Step "4" "Firewall rules"
$rules = @('EgyptTax HTTP (8088)', 'EgyptTax HTTPS (443)')
foreach ($r in $rules) {
    $rule = Get-NetFirewallRule -DisplayName $r -ErrorAction SilentlyContinue
    if ($rule) {
        $rule | Remove-NetFirewallRule -ErrorAction SilentlyContinue
        OK ("Removed firewall rule: " + $r)
    } else {
        Skip ("Not present: " + $r)
    }
}

# 5. SQL data
Step "5" "SQL Server databases (in SQLEXPRESS)"
function SqlRun([string] $q) {
    $r = sqlcmd -E -S '.\SQLEXPRESS' -b -Q $q 2>&1
    return @{ ExitCode = $LASTEXITCODE; Output = $r }
}

$sqlReachable = $false
$probe = SqlRun "SELECT 1"
if ($probe.ExitCode -eq 0) { $sqlReachable = $true }

if (-not $sqlReachable) {
    Skip "SQLEXPRESS unreachable (already gone or not running)"
} else {
    foreach ($db in 'EgyptTax', 'EgyptTax_Hangfire') {
        $exists = SqlRun ("SELECT name FROM sys.databases WHERE name = '" + $db + "'")
        if ($exists.Output -match $db) {
            $r = SqlRun ("ALTER DATABASE [" + $db + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + $db + "];")
            if ($r.ExitCode -eq 0) {
                OK ("Dropped database " + $db)
            } else {
                Warn ("Drop " + $db + " failed: " + $r.Output)
            }
        } else {
            Skip ("Database not present: " + $db)
        }
    }
}

# 6. Temp logs
Step "6" "Temp / log files"
$tempPatterns = @('daftarx-msi.log', 'daftarx-issue.token', 'is-*.tmp', 'egypttax-stdout.txt', 'egypttax-stderr.txt')
foreach ($pattern in $tempPatterns) {
    $found = Get-ChildItem $env:TEMP -Filter $pattern -Recurse -ErrorAction SilentlyContinue
    foreach ($f in $found) {
        try { Remove-Item $f.FullName -Recurse -Force -ErrorAction Stop; OK ("Removed " + $f.FullName) }
        catch { Warn ("Could not remove " + $f.FullName) }
    }
}

# 7. Optional: SQL Server Express
if ($Nuclear) {
    Step "7" "SQL Server Express (Nuclear mode notes)"
    $sqlSvc = Get-Service "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
    if ($sqlSvc) {
        Stop-Service "MSSQL`$SQLEXPRESS" -Force -ErrorAction SilentlyContinue
        OK "Stopped MSSQL`$SQLEXPRESS"
    }
    $sqlUninst = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue |
        ForEach-Object {
            $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
            if ($p.DisplayName -match 'SQL Server.*Express|SQL Server.*SQLEXPRESS') {
                [PSCustomObject]@{ Name = $p.DisplayName; UninstallString = $p.UninstallString }
            }
        }
    if ($sqlUninst) {
        Warn "SQL Server Express has its own uninstaller chain. Run each manually:"
        $sqlUninst | ForEach-Object {
            Write-Host ("      " + $_.Name) -ForegroundColor Yellow
            Write-Host ("      -> " + $_.UninstallString) -ForegroundColor DarkGray
        }
        Warn "Automated uninstall of SQL Server is fragile - use appwiz.cpl."
    } else {
        Skip "No SQL Express uninstall entry found"
    }

    $sqlData = 'C:\Program Files\Microsoft SQL Server'
    if (Test-Path $sqlData) {
        Warn ("Leaving " + $sqlData + " on disk - uninstall via appwiz.cpl first.")
    }
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  DaftarX uninstall complete." -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Reboot recommended." -ForegroundColor Yellow
Write-Host ""
