# DaftarX diagnostic. Single file, no params, run as admin.
# Produces daftarx-diagnostic.txt next to this script.
# ASCII only - safe for Telegram / email / weird transfers.

$ErrorActionPreference = "Continue"
$out = Join-Path $PSScriptRoot "daftarx-diagnostic.txt"
("DaftarX diagnostic - " + (Get-Date -Format o)) | Out-File $out

function Section($t) {
    "" | Add-Content $out
    "============================================================" | Add-Content $out
    ("  " + $t) | Add-Content $out
    "============================================================" | Add-Content $out
}
function Cap($label, [scriptblock]$sb) {
    "" | Add-Content $out
    ("--- " + $label + " ---") | Add-Content $out
    try   { (& $sb 2>&1 | Out-String).TrimEnd() | Add-Content $out }
    catch { ("ERROR: " + $_.Exception.Message)   | Add-Content $out }
}

Section "1. OS + admin"
Cap "Windows version"     { [System.Environment]::OSVersion; ("Is64BitOS: " + [System.Environment]::Is64BitOperatingSystem) }
Cap "Running as admin?"   { ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
Cap "Disk space C:"       { Get-PSDrive C | Select-Object Used, Free }

Section "2. EgyptTax service"
Cap "Get-Service"         { Get-Service EgyptTax -ErrorAction SilentlyContinue | Format-List Status, Name, StartType, DisplayName }
Cap "sc.exe qc"           { sc.exe qc EgyptTax 2>&1 }
Cap "sc.exe queryex"      { sc.exe queryex EgyptTax 2>&1 }
Cap "Try sc start"        { sc.exe start EgyptTax 2>&1 }
Cap "Port 8088"           { Get-NetTCPConnection -LocalPort 8088 -ErrorAction SilentlyContinue | Format-Table State, OwningProcess }

Section "3. Install folder"
Cap "Install dir present" { Test-Path 'C:\Program Files\EgyptTax' }
Cap "Top-level files"     { Get-ChildItem 'C:\Program Files\EgyptTax' -ErrorAction SilentlyContinue | Select-Object Name, Length | Format-Table -AutoSize }
Cap "appsettings.Production.json" { Get-Content 'C:\Program Files\EgyptTax\appsettings.Production.json' -ErrorAction SilentlyContinue }

Section "4. SQL Express"
Cap "MSSQL service"       { Get-Service "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue | Format-List Status, Name }
Cap "SELECT version"      { sqlcmd -E -S '.\SQLEXPRESS' -Q "SELECT @@VERSION" 2>&1 }
Cap "EgyptTax DB present" { sqlcmd -E -S '.\SQLEXPRESS' -Q "SELECT name FROM sys.databases WHERE name='EgyptTax'" 2>&1 }
Cap "Tables count"        { sqlcmd -E -S '.\SQLEXPRESS' -d EgyptTax -Q "SELECT COUNT(*) AS tbls FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'" 2>&1 }
Cap "NETWORK SERVICE login" { sqlcmd -E -S '.\SQLEXPRESS' -Q "SELECT name FROM sys.server_principals WHERE name = 'NT AUTHORITY\NETWORK SERVICE'" 2>&1 }
Cap "NETWORK SERVICE on EgyptTax" { sqlcmd -E -S '.\SQLEXPRESS' -d EgyptTax -Q "SELECT name FROM sys.database_principals WHERE name = 'NT AUTHORITY\NETWORK SERVICE'" 2>&1 }

Section "5. License directory"
Cap "License dir tree"    { Get-ChildItem 'C:\ProgramData\DaftarX' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName, Length | Format-Table -AutoSize }
Cap "License-gate crash log" {
    $p = 'C:\ProgramData\DaftarX\license\license-gate-crash.log'
    if (Test-Path $p) { Get-Content $p } else { "(not present)" }
}

Section "6. Service log (Serilog)"
Cap "Last 80 lines" {
    $f = Get-ChildItem 'C:\Program Files\EgyptTax\logs' -ErrorAction SilentlyContinue |
         Sort-Object LastWriteTime -Desc | Select-Object -First 1
    if ($f) { ("File: " + $f.FullName); ""; Get-Content $f.FullName -Tail 80 } else { "(no log file)" }
}

Section "7. Manual run - capture stderr from EgyptTax.Web.exe"
Cap "Run for 8 seconds + capture all output" {
    $exe = 'C:\Program Files\EgyptTax\EgyptTax.Web.exe'
    if (-not (Test-Path $exe)) { return "(exe not present)" }

    $stdout = Join-Path $env:TEMP "egypttax-stdout.txt"
    $stderr = Join-Path $env:TEMP "egypttax-stderr.txt"
    "" | Out-File $stdout
    "" | Out-File $stderr
    $p = Start-Process -FilePath $exe -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden
    Start-Sleep -Seconds 8
    if (-not $p.HasExited) { $p.Kill() | Out-Null }
    ("Exit code: " + $p.ExitCode)
    ""
    "--- stdout ---"
    Get-Content $stdout -ErrorAction SilentlyContinue
    "--- stderr ---"
    Get-Content $stderr -ErrorAction SilentlyContinue
}

Section "8. MSI install log"
Cap "Last 120 lines of CA-related entries" {
    $msi = Get-ChildItem $env:TEMP -Filter 'daftarx-msi.log' -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTime -Desc | Select-Object -First 1
    if ($msi) {
        ("Log: " + $msi.FullName); ""
        Get-Content $msi.FullName |
          Select-String 'Return value|CustomAction|GrantSqlPermissions|ApplyMigrationsAndSeed|ApplyConfig|RegisterService|SetupHttps|VerifyHealth|Error 1603|ERROR' |
          Select-Object -Last 120
    } else { "(no MSI log in TEMP)" }
}

Section "9. Application Event Log"
Cap "Last 10 .NET / EgyptTax errors" {
    Get-WinEvent -LogName Application -MaxEvents 500 -ErrorAction SilentlyContinue |
      Where-Object { $_.LevelDisplayName -in 'Error','Critical' -and ($_.ProviderName -like '.NET*' -or $_.ProviderName -eq 'Application Error' -or $_.Message -match 'EgyptTax|DaftarX') } |
      Select-Object -First 10 |
      Format-List TimeCreated, ProviderName, LevelDisplayName, Message
}

Section "10. HTTP probe"
Cap "GET /api/v1/health/live" {
    try { (Invoke-WebRequest http://localhost:8088/api/v1/health/live -UseBasicParsing -TimeoutSec 5).Content } catch { ("ERROR: " + $_.Exception.Message) }
}
Cap "GET /" {
    try {
        $r = Invoke-WebRequest http://localhost:8088/ -UseBasicParsing -TimeoutSec 5
        ("StatusCode: " + $r.StatusCode); "Content (first 800 chars):"; $r.Content.Substring(0, [Math]::Min(800, $r.Content.Length))
    } catch {
        ("ERROR: " + $_.Exception.Message)
        if ($_.Exception.Response) {
            ("StatusCode: " + [int]$_.Exception.Response.StatusCode)
            try {
                $body = (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
                "Body (first 800 chars):"; $body.Substring(0, [Math]::Min(800, $body.Length))
            } catch { "(could not read body)" }
        }
    }
}

"" | Add-Content $out
"=== END ===" | Add-Content $out

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  Diagnostic written to:" -ForegroundColor Green
Write-Host ("    " + $out) -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Send daftarx-diagnostic.txt back to support." -ForegroundColor Yellow
Write-Host ""
notepad.exe $out
