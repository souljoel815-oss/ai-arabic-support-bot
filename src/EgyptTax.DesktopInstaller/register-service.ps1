# DaftarX Windows Service registration.
#
# Run by the MSI's RegisterService deferred CustomAction (via
# WixQuietExec). Must exit 0 on success or the MSI rolls back a
# ~100 MB install, so we:
#   * log everything to %ProgramData%\DaftarX\service-install.log
#     for diagnosability,
#   * use New-Service (not sc.exe @args splatting) — Windows
#     PowerShell 5.1 mangles embedded quotes when passing args to
#     native exes, which was the original 1603 root cause,
#   * keep service-start failures NON-fatal: registering the
#     service is the install-time contract; if Kestrel can't bind
#     on first boot the customer can `sc start DaftarX` later.
# Re-running over an existing install stops + deletes the prior
# service first so the new binPath / args take.

# No parameters — we used to take -InstallFolder from the MSI but
# [INSTALLFOLDER] resolves with a trailing backslash, and "...\"
# in a command line is an escaped quote per CommandLineToArgvW,
# which corrupted the argument and produced 1603 rollbacks. The
# script lives next to the install root so $PSScriptRoot is the
# authoritative source. The MSI invokes us via -File so it's set.

$svcName = 'DaftarX'
$dataDir = 'C:\ProgramData\DaftarX'
$logPath = Join-Path $dataDir 'service-install.log'

# Bootstrap the data directory + log BEFORE anything that can fail,
# so even early errors get captured.
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}
function Log($msg) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $msg
    Add-Content -Path $logPath -Value $line -Encoding utf8
}

Log '----- register-service.ps1 begin -----'
Log "PSScriptRoot: '$PSScriptRoot'"
Log "PSCommandPath: '$PSCommandPath'"

try {
    $installFolder = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($installFolder) -and -not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
        # PSScriptRoot is empty when -Command is used (we use -File so
        # this is belt-and-braces); derive from the script's own path.
        $installFolder = Split-Path -Parent $PSCommandPath
    }
    $installFolder = $installFolder.TrimEnd('\')
    $webExe = Join-Path $installFolder 'web\EgyptTax.Web.exe'
    $dbPath = Join-Path $dataDir 'daftarx.db'
    Log "Resolved installFolder: $installFolder"
    Log "webExe: $webExe"
    Log "dbPath: $dbPath"

    if (-not (Test-Path $webExe)) {
        Log "FATAL: EgyptTax.Web.exe not found at $webExe"
        # This IS a real install failure — the bundled web didn't
        # land where we expected. Let the MSI roll back.
        exit 2
    }

    # Stop + delete any prior instance so the new binPath takes.
    $existing = Get-Service -Name $svcName -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        Log "Existing service found (status=$($existing.Status)); stopping + deleting."
        if ($existing.Status -ne 'Stopped') {
            try { Stop-Service -Name $svcName -Force -ErrorAction Stop } catch { Log "Stop-Service: $($_.Exception.Message)" }
            $deadline = (Get-Date).AddSeconds(30)
            while ($true) {
                $s = Get-Service -Name $svcName -ErrorAction SilentlyContinue
                if ($null -eq $s -or $s.Status -eq 'Stopped' -or (Get-Date) -ge $deadline) { break }
                Start-Sleep -Milliseconds 500
            }
        }
        # sc.exe delete with NO 2>&1 (5.1 turns native stderr into a
        # terminating error). We only need the exit code.
        $null = & sc.exe delete $svcName
        Log "sc.exe delete exit=$LASTEXITCODE"
        $deadline = (Get-Date).AddSeconds(30)
        while ($null -ne (Get-Service -Name $svcName -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
        }
    }

    # Build the BinaryPathName as ONE string. New-Service hands this
    # directly to the SCM — no PowerShell native-command parser, no
    # sc.exe binPath= quirks. The EXE path is quoted to survive the
    # "Program Files" space; argument values use plain double quotes
    # because the SCM's CommandLineToArgvW parses them normally.
    $bindUrl = 'http://0.0.0.0:50063'
    $connStr = "Data Source=$dbPath"
    $binaryPath = '"{0}" --urls={1} --ConnectionStrings:EgyptTax="{2}"' -f $webExe, $bindUrl, $connStr
    Log "BinaryPathName: $binaryPath"

    New-Service -Name $svcName `
                -BinaryPathName $binaryPath `
                -DisplayName 'DaftarX' `
                -Description 'DaftarX accounting web service (port 50063)' `
                -StartupType Automatic | Out-Null
    Log 'New-Service: created.'

    # Restart-on-failure (New-Service doesn't expose this).
    $null = & sc.exe failure $svcName reset= 86400 actions= restart/5000/restart/5000/restart/5000
    Log "sc.exe failure exit=$LASTEXITCODE"

    # Start the service. NON-fatal — port may be temporarily bound,
    # AV may be scanning the EXE on first launch, etc. The service
    # is StartupType=Automatic so it'll come up on next boot
    # regardless; we don't want to roll back a 100 MB install over
    # a transient first-start hiccup.
    try {
        Start-Service -Name $svcName -ErrorAction Stop
        Log 'Start-Service: started.'
    } catch {
        Log "Start-Service WARNING (non-fatal): $($_.Exception.Message)"
    }

    Log '----- register-service.ps1 success -----'
    exit 0
}
catch {
    Log "FATAL exception: $($_.Exception.Message)"
    Log $_.ScriptStackTrace
    # Still exit 0 — the worst case is the service didn't register;
    # the desktop launcher will fall back to spawning its own child
    # web process so the customer can at least use the app locally.
    # The log file tells us what to fix on the next service-install
    # attempt (manual repair via the same script).
    exit 0
}
