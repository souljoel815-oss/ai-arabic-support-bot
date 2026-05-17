# One-shot build for DaftarX-Client-Setup.msi.
# Run from the repo root:
#   pwsh src/EgyptTax.DesktopClientInstaller/build-client-installer.ps1
#
# Produces:
#   src/EgyptTax.DesktopClientInstaller/bin/Release/DaftarX-Client-Setup.msi (~30 MB)
#
# The MSI ships ONLY the DaftarX desktop launcher + self-contained .NET
# runtime. No bundled web app, no service. First launch prompts for
# the LAN server URL and opens a window pointing at it.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

Write-Host '[1/3] Cleaning prior client publish...' -ForegroundColor Cyan
Remove-Item -Recurse -Force "$repoRoot\publish\client" -ErrorAction SilentlyContinue

Write-Host '[2/3] Publishing EgyptTax.Desktop (self-contained, win-x64)...' -ForegroundColor Cyan
dotnet publish "$repoRoot\src\EgyptTax.Desktop\EgyptTax.Desktop.csproj" -c Release -r win-x64 --self-contained -o "$repoRoot\publish\client" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }

# CRITICAL: the Desktop project's AfterPublish target tries to copy
# a bundled web/ into the publish dir. For the CLIENT MSI we must
# delete it — the launcher detects "no bundled web" at runtime and
# enters client mode, prompting for the server URL. If we ship web/
# the launcher would try to spawn a local Kestrel and we'd be back
# to the per-machine-DB problem.
if (Test-Path "$repoRoot\publish\client\web") {
    Write-Host '  Removing accidentally-bundled web/ folder...' -ForegroundColor Yellow
    Remove-Item -Recurse -Force "$repoRoot\publish\client\web"
}

# Sanity-check: DaftarX.exe must be there.
if (-not (Test-Path "$repoRoot\publish\client\DaftarX.exe")) {
    throw 'publish\client\DaftarX.exe missing after publish'
}

Write-Host '[3/3] Building DaftarX-Client-Setup.msi...' -ForegroundColor Cyan
Remove-Item -Recurse -Force "$repoRoot\src\EgyptTax.DesktopClientInstaller\bin", "$repoRoot\src\EgyptTax.DesktopClientInstaller\obj" -ErrorAction SilentlyContinue
dotnet build "$repoRoot\src\EgyptTax.DesktopClientInstaller\EgyptTax.DesktopClientInstaller.wixproj" -c Release -p:HarvestPublishDir="$repoRoot\publish\client" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Client installer build failed' }

$msi = "$repoRoot\src\EgyptTax.DesktopClientInstaller\bin\Release\DaftarX-Client-Setup.msi"
$sizeMb = [math]::Round((Get-Item $msi).Length / 1MB, 1)
Write-Host ''
Write-Host "DONE: $msi ($sizeMb MB)" -ForegroundColor Green
