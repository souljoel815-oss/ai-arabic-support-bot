<#
.SYNOPSIS
    Registers (or updates) the EgyptTax Windows service idempotently.

.DESCRIPTION
    Replaces the simple `sc.exe create EgyptTax ...` MSI custom
    action with a PowerShell wrapper that's tolerant of the three
    pre-existing-service states the simple form trips on:

      1. Service exists + running          → stop + reconfigure
      2. Service exists + marked-for-delete → wait for kernel cleanup
                                              (up to 30s); retry create
      3. Service doesn't exist             → straight create

    Without this, sc.exe create fails with error 1072
    "ERROR_SERVICE_MARKED_FOR_DELETE" when the operator runs the
    MSI on a machine that still has remnants of a previous failed
    install — and the marked-for-delete state can persist for
    minutes if any process holds an open handle to the service.

.PARAMETER InstallFolder
    Derived from $PSScriptRoot — install folder where
    EgyptTax.Web.exe lives.
#>
param()

$ErrorActionPreference = "Stop"
$serviceName = "EgyptTax"
$binPath     = Join-Path $PSScriptRoot "EgyptTax.Web.exe"
$displayName = "EgyptTax - Tax compliance service"
$account     = "NT AUTHORITY\NetworkService"

function Test-ServiceMarkedForDelete {
    # sc.exe queryex returns a textual block; "service has been marked
    # for deletion" is the canonical phrase. Status PENDING_DELETE is
    # the equivalent state.
    $output = & sc.exe queryex $serviceName 2>&1
    return ($output -match "marked for deletion" -or $output -match "PENDING_DELETE")
}

function Test-ServiceExists {
    & sc.exe query $serviceName 2>&1 | Out-Null
    return ($LASTEXITCODE -eq 0)
}

# Wait up to 30s for any pending deletion to clear. Most MSI re-runs
# clear within 1-2s; the loop is defensive against a slow handle close.
$waited = 0
while ((Test-ServiceMarkedForDelete) -and $waited -lt 30) {
    Write-Host "EgyptTax service marked for deletion; waiting for kernel cleanup ($waited/30s)..."
    Start-Sleep -Seconds 1
    $waited++
}

if (Test-ServiceMarkedForDelete) {
    Write-Error "EgyptTax service still marked for deletion after 30s. A reboot may be required to clear the marker; re-run the install after reboot."
    exit 1
}

if (Test-ServiceExists) {
    # Pre-existing live service — update its config instead of creating.
    # `sc.exe config` modifies an existing service in-place; safe to
    # run even if config matches.
    Write-Host "EgyptTax service exists; updating config in place."
    & sc.exe stop $serviceName 2>&1 | Out-Null
    Start-Sleep -Seconds 1
    & sc.exe config $serviceName binPath= "`"$binPath`"" start= auto DisplayName= "$displayName" obj= "$account"
    if ($LASTEXITCODE -ne 0) { Write-Error "sc.exe config failed with exit $LASTEXITCODE"; exit $LASTEXITCODE }
}
else {
    Write-Host "Creating EgyptTax service."
    & sc.exe create $serviceName binPath= "`"$binPath`"" start= auto DisplayName= "$displayName" obj= "$account"
    if ($LASTEXITCODE -ne 0) { Write-Error "sc.exe create failed with exit $LASTEXITCODE"; exit $LASTEXITCODE }
}

# Start the service. sc.exe start returns immediately after asking
# SCM to launch — the lifecycle wait happens inside the service host
# (UseWindowsService in Program.cs).
Write-Host "Starting EgyptTax service."
& sc.exe start $serviceName
if ($LASTEXITCODE -ne 0) {
    Write-Warning "sc.exe start exited $LASTEXITCODE (service may already be Running). Continuing."
}

Write-Host "EgyptTax service registration complete."
exit 0
