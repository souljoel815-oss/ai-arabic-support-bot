#requires -RunAsAdministrator
<#
  trust-cert.ps1 — Workstation-side companion to the DaftarX install.

  Asks for:
    - Server IP address (e.g. 192.168.1.50)
    - Server domain name (e.g. daftarx.local)

  Then:
    1. Adds an entry to the local hosts file so https://<domain>/
       resolves to the server.
    2. Downloads the server's HTTPS public certificate from
       http://<ip>:5000/daftarx-cert.cer
    3. Installs the cert in LocalMachine\Root so the browser stops
       warning "Your connection is not private".

  Idempotent: re-running replaces any prior DaftarX hosts entry +
  re-installs the cert.
#>

$ErrorActionPreference = 'Stop'

# ---------------- Inputs ----------------
$serverIp = Read-Host 'Server IP address (e.g. 192.168.1.50)'
if ([string]::IsNullOrWhiteSpace($serverIp)) {
    Write-Host 'No IP entered. Aborting.' -ForegroundColor Yellow
    exit 1
}
if ($serverIp -notmatch '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$') {
    Write-Host 'That does not look like a valid IPv4 address.' -ForegroundColor Yellow
    exit 1
}

$domainPrompt = Read-Host 'Server domain name (Enter to accept default: daftarx.local)'
$domain = if ([string]::IsNullOrWhiteSpace($domainPrompt)) { 'daftarx.local' } else { $domainPrompt.Trim() }

Write-Host ''
Write-Host '============================================================'
Write-Host '  Configuring this workstation for DaftarX'
Write-Host ('    Server IP:     ' + $serverIp)
Write-Host ('    Server domain: ' + $domain)
Write-Host '============================================================'
Write-Host ''

# ---------------- Step 1: hosts file ----------------
Write-Host 'Step 1/3: updating hosts file ...' -ForegroundColor Cyan
$hostsPath = Join-Path $env:WINDIR 'System32\drivers\etc\hosts'
$marker = '# DaftarX'
try {
    $hostsLines = Get-Content $hostsPath -ErrorAction Stop
    # Drop any prior DaftarX entries so re-running replaces in place
    $hostsLines = $hostsLines | Where-Object { $_ -notmatch [regex]::Escape($marker) }
    $hostsLines += "$serverIp`t$domain`t$marker"
    Set-Content -Path $hostsPath -Value $hostsLines -Encoding ASCII -Force
    Write-Host ('  OK: ' + $serverIp + ' -> ' + $domain) -ForegroundColor Green
} catch {
    Write-Host ('  FAIL: could not write hosts file: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}

# Flush DNS cache so the new mapping takes effect immediately
ipconfig /flushdns | Out-Null

# ---------------- Step 2: download cert ----------------
Write-Host ''
Write-Host 'Step 2/3: downloading DaftarX certificate from server ...' -ForegroundColor Cyan
$certPath = Join-Path $env:TEMP 'daftarx-cert.cer'
try {
    Invoke-WebRequest -Uri ("http://$serverIp" + ':5000/daftarx-cert.cer') `
        -OutFile $certPath -UseBasicParsing -TimeoutSec 15
    Write-Host '  OK' -ForegroundColor Green
} catch {
    Write-Host ('  FAIL: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ''
    Write-Host '  Make sure:'
    Write-Host ('    - Server is reachable at http://' + $serverIp + ':5000/')
    Write-Host '    - DaftarX service is Running on the server'
    Write-Host '    - Server firewall allows port 5000'
    exit 1
}

# ---------------- Step 3: install cert in Trusted Root ----------------
Write-Host ''
Write-Host 'Step 3/3: installing certificate in Trusted Root ...' -ForegroundColor Cyan
try {
    $output = & certutil.exe -addstore 'Root' $certPath 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) {
        Write-Host '  OK' -ForegroundColor Green
    } else {
        Write-Host ('  FAIL: certutil exited ' + $LASTEXITCODE) -ForegroundColor Red
        Write-Host $output
        exit 1
    }
} finally {
    Remove-Item $certPath -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host '============================================================' -ForegroundColor Green
Write-Host '  Done! Open DaftarX in your browser at:'                    -ForegroundColor Green
Write-Host ('    https://' + $domain + '/')                              -ForegroundColor Green
Write-Host ''                                                            -ForegroundColor Green
Write-Host '  No certificate warnings. Bookmark it!'                     -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor Green
Write-Host ''
