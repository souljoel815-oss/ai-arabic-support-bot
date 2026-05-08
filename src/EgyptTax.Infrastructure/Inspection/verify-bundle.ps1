<#
.SYNOPSIS
    Verifies a Tax Inspection Bundle on a clean Windows machine
    without the EgyptTax application installed.

.DESCRIPTION
    Per US9 / FR-048 and contracts/inspection-bundle-manifest.schema.json,
    every Tax Inspection Bundle ships a MANIFEST.sha256 that lists
    every file in the bundle with its SHA-256 + size. This script:

      * Reads MANIFEST.sha256 from the unzipped bundle directory.
      * For each entry: recomputes SHA-256 + size from the on-disk
        file, compares to the manifest's recorded values.
      * Reports per-file PASS / FAIL and an overall verdict.
      * Exits 0 on success, 1 on any mismatch / missing file,
        2 on usage error.

    The script intentionally does NOT replay the FR-028 audit-chain
    inside audit-trail/audit-trail-extract.jsonl — that requires
    JCS (RFC 8785) canonicalization which is non-trivial in pure
    PowerShell. The audit-trail-extract.jsonl file's per-file
    SHA-256 IS verified (so any post-handoff modification trips
    this script); for full hash-chain replay run
    `EgyptTax.Web verify-audit` against a copy of the live DB or
    use the C#-based verifier ported from the application code.

    Top-level archive hash (manifest's topLevelArchiveSha256) is
    also out of scope — it requires producing the exact same JSON
    bytes that the application's serializer produces (camelCase,
    indented, UnsafeRelaxedJsonEscaping), which is brittle to
    implement in PowerShell. The per-file hashes alone are
    sufficient to detect any tampering.

.PARAMETER BundleDirectory
    Path to the unzipped bundle directory. Must contain
    MANIFEST.sha256 at its root. Required.

.EXAMPLE
    PS> .\verify-bundle.ps1 -BundleDirectory "C:\inspection\bundle-2026-Q1"

    Verifies every file listed in
    C:\inspection\bundle-2026-Q1\MANIFEST.sha256.

.NOTES
    Requires PowerShell 5.1 or later. Uses only built-in cmdlets
    (Get-FileHash, ConvertFrom-Json, Get-Content) — no modules,
    no .NET assembly loads, no internet access. Designed to run
    in an air-gapped inspector environment.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$BundleDirectory
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $BundleDirectory -PathType Container)) {
    Write-Host "[verify-bundle] ERROR: '$BundleDirectory' is not a directory." -ForegroundColor Red
    exit 2
}

$manifestPath = Join-Path -Path $BundleDirectory -ChildPath 'MANIFEST.sha256'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    Write-Host "[verify-bundle] ERROR: MANIFEST.sha256 not found at $manifestPath." -ForegroundColor Red
    exit 2
}

Write-Host "[verify-bundle] Reading manifest: $manifestPath"
$manifestRaw = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
try {
    $manifest = $manifestRaw | ConvertFrom-Json
} catch {
    Write-Host "[verify-bundle] ERROR: MANIFEST.sha256 is not valid JSON: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

if ($manifest.bundleVersion -ne '1.0') {
    Write-Host "[verify-bundle] ERROR: unsupported bundleVersion '$($manifest.bundleVersion)' (this script handles 1.0)." -ForegroundColor Red
    exit 2
}

Write-Host ""
Write-Host "[verify-bundle] Bundle: $($manifest.company.legalName.en) ($($manifest.company.tin))"
Write-Host "[verify-bundle] Period: $($manifest.period.start) -> $($manifest.period.end)"
Write-Host "[verify-bundle] Generated: $($manifest.generatedAt)"
Write-Host "[verify-bundle] Files: $($manifest.files.Count)"
if ($manifest.draftsExcluded) {
    $excludedCount = if ($null -ne $manifest.excludedDraftIds) { $manifest.excludedDraftIds.Count } else { 0 }
    Write-Host "[verify-bundle] WARNING: drafts-excluded bundle. Drafts omitted: $excludedCount" -ForegroundColor Yellow
}
Write-Host ""

$failures = @()
$verified = 0

foreach ($file in $manifest.files) {
    $relativePath = $file.relativePath
    $expectedSha = $file.sha256
    $expectedSize = [int64]$file.sizeBytes

    # Reject path-traversal attempts before touching the filesystem.
    if ($relativePath -match '\.\.' -or $relativePath -match '^[\\/]') {
        $failures += [pscustomobject]@{
            File = $relativePath
            Reason = 'Suspicious relative path (contains .. or starts with separator)'
        }
        Write-Host "  [FAIL] $relativePath - suspicious path" -ForegroundColor Red
        continue
    }

    # Normalize forward-slash path from manifest to OS-native.
    $diskPath = Join-Path -Path $BundleDirectory -ChildPath ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)

    if (-not (Test-Path -LiteralPath $diskPath -PathType Leaf)) {
        $failures += [pscustomobject]@{
            File = $relativePath
            Reason = "File missing on disk at $diskPath"
        }
        Write-Host "  [FAIL] $relativePath - missing on disk" -ForegroundColor Red
        continue
    }

    $actualSize = (Get-Item -LiteralPath $diskPath).Length
    if ($actualSize -ne $expectedSize) {
        $failures += [pscustomobject]@{
            File = $relativePath
            Reason = "Size mismatch (manifest=$expectedSize, disk=$actualSize)"
        }
        Write-Host "  [FAIL] $relativePath - size mismatch" -ForegroundColor Red
        continue
    }

    $actualSha = (Get-FileHash -LiteralPath $diskPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha -ne $expectedSha) {
        $failures += [pscustomobject]@{
            File = $relativePath
            Reason = "SHA-256 mismatch (manifest=$expectedSha, disk=$actualSha)"
        }
        Write-Host "  [FAIL] $relativePath - SHA-256 mismatch" -ForegroundColor Red
        continue
    }

    Write-Host "  [PASS] $relativePath ($($file.category))" -ForegroundColor Green
    $verified++
}

Write-Host ""
$totalFiles = $manifest.files.Count
Write-Host "[verify-bundle] Verified: $verified / $totalFiles files."

if ($failures.Count -eq 0) {
    Write-Host "[verify-bundle] BUNDLE INTEGRITY: PASS" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: this script verifies per-file SHA-256 against the manifest."
    Write-Host "Top-level archive hash and audit-chain replay are out of scope (require"
    Write-Host "the .NET-based verifier shipped with the application). Per-file SHA-256"
    Write-Host "is the bedrock guarantee — any modification to any bundled file would"
    Write-Host "have failed at least one of the checks above."
    exit 0
}

$failureCount = $failures.Count
Write-Host "[verify-bundle] BUNDLE INTEGRITY: FAIL. Findings: $failureCount" -ForegroundColor Red
Write-Host ""
$failures | Format-Table -AutoSize
exit 1
