<#
.SYNOPSIS
    T257 — health-readiness gate for the EgyptTax MSI installer.

.DESCRIPTION
    Polls https://localhost/api/v1/health/ready up to 10 times
    with a 1-second gap between attempts. Exits 0 only when the
    endpoint returns HTTP 200 with body { "status": "Healthy" }.
    Any other outcome exits non-zero, which the MSI installer's
    `Return="check"` policy treats as a failure + rolls the
    install back.

    The point: the operator's "install complete" screen MUST NOT
    fire while the service is still booting up — the readiness
    probe is the canonical "is the service genuinely serving
    requests" signal. A fresh install where SQL is unreachable
    or the seed CLI failed silently would otherwise produce a
    "complete" screen + an immediately-broken application; this
    script prevents that.

.NOTES
    The TLS cert at install time is the kestrel dev cert; this
    script accepts it. Operators replacing the cert post-install
    don't need to re-verify (the readiness check has already
    passed once).
#>

$ErrorActionPreference = 'Stop'
$Url = 'https://localhost/api/v1/health/ready'
$MaxAttempts = 10
$DelaySeconds = 1

# Accept the development self-signed cert at install time.
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            try {
                $payload = $response.Content | ConvertFrom-Json
                if ($payload.status -eq 'Healthy') {
                    Write-Host "EgyptTax readiness probe healthy on attempt $attempt."
                    exit 0
                }
                Write-Host "Attempt $attempt — readiness payload: $($response.Content)"
            }
            catch {
                Write-Host "Attempt $attempt — non-JSON response: $($response.Content)"
            }
        }
        else {
            Write-Host "Attempt $attempt — HTTP $($response.StatusCode)"
        }
    }
    catch {
        Write-Host "Attempt $attempt — error: $($_.Exception.Message)"
    }

    if ($attempt -lt $MaxAttempts) {
        Start-Sleep -Seconds $DelaySeconds
    }
}

Write-Error "EgyptTax readiness probe never reported Healthy after $MaxAttempts attempts."
exit 1
