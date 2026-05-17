# One-shot build for DaftarX-Setup.msi.
# Run from the repo root:
#   pwsh src/EgyptTax.DesktopInstaller/build-installer.ps1
#
# Produces:
#   src/EgyptTax.DesktopInstaller/bin/Release/DaftarX-Setup.msi (~110 MB)
#
# The MSI installs DaftarX desktop + bundled self-contained ASP.NET
# Core web host + registers a Windows Service that auto-starts on
# boot and listens on 0.0.0.0:50063. Customer needs zero
# prerequisites (no .NET install, no SQL Server, nothing).

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

Write-Host '[1/4] Cleaning prior publish output...' -ForegroundColor Cyan
Remove-Item -Recurse -Force "$repoRoot\publish\web", "$repoRoot\publish\desktop" -ErrorAction SilentlyContinue

Write-Host '[2/4] Publishing EgyptTax.Web (self-contained, win-x64)...' -ForegroundColor Cyan
dotnet publish "$repoRoot\src\EgyptTax.Web\EgyptTax.Web.csproj" -c Release -r win-x64 --self-contained -o "$repoRoot\publish\web" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Web publish failed' }

Write-Host '[3/4] Publishing EgyptTax.Desktop (self-contained, win-x64)...' -ForegroundColor Cyan
dotnet publish "$repoRoot\src\EgyptTax.Desktop\EgyptTax.Desktop.csproj" -c Release -r win-x64 --self-contained -o "$repoRoot\publish\desktop" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }

# AfterPublish target in Desktop's csproj can't see across -o output
# roots, so copy the self-contained Web bundle into publish/desktop/web
# explicitly. (If the target later finds a stale framework-dependent
# Web in bin/Release/net8.0/publish/, it'd ship that instead — clearing
# the directory first guarantees the right shape.)
Remove-Item -Recurse -Force "$repoRoot\publish\desktop\web" -ErrorAction SilentlyContinue
Copy-Item -Recurse "$repoRoot\publish\web" "$repoRoot\publish\desktop\web"
if (-not (Test-Path "$repoRoot\publish\desktop\web\hostfxr.dll")) {
    throw 'web/hostfxr.dll missing — bundled Web is not self-contained'
}

Write-Host '[4/4] Building DaftarX-Setup.msi...' -ForegroundColor Cyan
Remove-Item -Recurse -Force "$repoRoot\src\EgyptTax.DesktopInstaller\bin", "$repoRoot\src\EgyptTax.DesktopInstaller\obj" -ErrorAction SilentlyContinue
dotnet build "$repoRoot\src\EgyptTax.DesktopInstaller\EgyptTax.DesktopInstaller.wixproj" -c Release -p:HarvestPublishDir="$repoRoot\publish\desktop" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }

$msi = "$repoRoot\src\EgyptTax.DesktopInstaller\bin\Release\DaftarX-Setup.msi"
$sizeMb = [math]::Round((Get-Item $msi).Length / 1MB, 1)
Write-Host ''
Write-Host "DONE: $msi ($sizeMb MB)" -ForegroundColor Green
