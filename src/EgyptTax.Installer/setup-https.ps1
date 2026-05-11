#requires -Version 5.1
<#
  setup-https.ps1 — generate self-signed HTTPS certificate for DaftarX
  and configure Kestrel to bind on port 443.

  Invoked by the MSI as a post-install custom action with:
    -InternalDomain  <e.g. daftarx.local — optional>

  What it does:
    1. Build SAN list: localhost, 127.0.0.1, MACHINE_NAME,
       MACHINE_IP[, $InternalDomain]
    2. Create self-signed cert in CurrentUser\My
    3. Export to C:\ProgramData\DaftarX\cert.pfx (password protected)
    4. Install public cert in LocalMachine\Root so the SERVER browser
       (and the EgyptTax service itself) trust it
    5. Export public cert to C:\ProgramData\DaftarX\daftarx-cert.cer
       so workstations can also install it
    6. Patch appsettings.Production.json to bind Kestrel on HTTPS:443

  Idempotent: re-running replaces the cert + appsettings binding.
#>

param(
    [string]$InstallFolder = 'C:\Program Files\EgyptTax',
    [string]$InternalDomain = ''
)

$ErrorActionPreference = 'Stop'
$dataDir = 'C:\ProgramData\DaftarX'
if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir -Force | Out-Null }

$logPath = Join-Path $dataDir 'setup-https.log'
function Log($msg) {
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $msg" | Tee-Object -FilePath $logPath -Append | Out-Host
}

Log "==== setup-https.ps1 starting (InternalDomain='$InternalDomain') ===="

# Build SAN list
$hostName = [System.Net.Dns]::GetHostName()
$ipv4 = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.PrefixOrigin -ne 'WellKnown' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -First 1).IPAddress

$dnsNames = @('localhost', $hostName)
if ($InternalDomain -and $InternalDomain -ne '') {
    $dnsNames += $InternalDomain
}
$ipAddresses = @('127.0.0.1')
if ($ipv4) { $ipAddresses += $ipv4 }

Log "DNS SANs: $($dnsNames -join ', ')"
Log "IP SANs:  $($ipAddresses -join ', ')"

# Generate self-signed cert (5-year validity)
$cert = New-SelfSignedCertificate `
    -DnsName ($dnsNames + $ipAddresses) `
    -CertStoreLocation 'Cert:\LocalMachine\My' `
    -FriendlyName 'DaftarX HTTPS Certificate' `
    -KeyAlgorithm RSA -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears(5) `
    -KeyUsage DigitalSignature, KeyEncipherment `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.1') `
    -Subject "CN=DaftarX, O=DaftarX, C=EG"

Log "Generated cert thumbprint: $($cert.Thumbprint)"

# Export PFX (Kestrel reads this directly)
$pfxPassword = ConvertTo-SecureString -String 'DaftarXCert#2026' -Force -AsPlainText
$pfxPath = Join-Path $dataDir 'cert.pfx'
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword -Force | Out-Null
Log "Exported PFX to $pfxPath"

# Export public cert (.cer) for workstations to install in their Root store
$cerPath = Join-Path $dataDir 'daftarx-cert.cer'
Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
Log "Exported public cert to $cerPath"

# Trust the cert on this machine (LocalMachine\Root)
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
$rootStore.Open('ReadWrite')
# Remove any older DaftarX cert in Root
$rootStore.Certificates | Where-Object { $_.Subject -eq $cert.Subject } | ForEach-Object {
    Log "Removing older Root cert thumbprint $($_.Thumbprint)"
    $rootStore.Remove($_)
}
$rootStore.Add($cert)
$rootStore.Close()
Log "Installed cert in LocalMachine\Root (server browser trusts it)."

# Patch appsettings.Production.json for HTTPS Kestrel binding
$appsettingsPath = Join-Path $InstallFolder 'appsettings.Production.json'
if (Test-Path $appsettingsPath) {
    $json = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    if (-not $json.Kestrel) {
        Add-Member -InputObject $json -MemberType NoteProperty -Name 'Kestrel' -Value ([pscustomobject]@{}) -Force
    }
    $json.Kestrel = [pscustomobject]@{
        Endpoints = [pscustomobject]@{
            Http  = [pscustomobject]@{ Url = 'http://*:5000' }
            Https = [pscustomobject]@{
                Url = 'https://*:443'
                Certificate = [pscustomobject]@{
                    Path     = $pfxPath
                    Password = 'DaftarXCert#2026'
                }
            }
        }
    }
    $json | ConvertTo-Json -Depth 10 | Out-File $appsettingsPath -Encoding UTF8
    Log "Patched appsettings.Production.json with Kestrel HTTPS endpoint."
} else {
    Log "WARN: appsettings.Production.json not found at $appsettingsPath"
}

# Open firewall port 443 (defensive — MSI already does it via WiX, but no harm)
try {
    netsh advfirewall firewall delete rule name='DaftarX HTTPS 443' 2>&1 | Out-Null
    netsh advfirewall firewall add rule name='DaftarX HTTPS 443' dir=in action=allow protocol=TCP localport=443 | Out-Null
    Log "Firewall rule for TCP 443 ensured."
} catch {
    Log "WARN: failed to set firewall rule: $($_.Exception.Message)"
}

# Restart EgyptTax service so Kestrel picks up the new HTTPS binding
try {
    Restart-Service -Name EgyptTax -Force -ErrorAction Stop
    Log "EgyptTax service restarted."
} catch {
    Log "WARN: could not restart service: $($_.Exception.Message)"
}

Log "==== setup-https.ps1 done ===="

# Friendly URLs for the customer:
Log "Server can be reached at:"
foreach ($n in $dnsNames) { Log "  https://$n/" }
foreach ($i in $ipAddresses) { Log "  https://$i/" }

exit 0
