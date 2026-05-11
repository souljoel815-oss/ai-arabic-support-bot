<#
.SYNOPSIS
    Grants the EgyptTax Windows service account access to the
    EgyptTax database.

.DESCRIPTION
    The EgyptTax service runs as NT AUTHORITY\NetworkService (see
    register-service.ps1). On a fresh SQL Server Express install,
    NetworkService has NO SQL login by default — only
    BUILTIN\Administrators is sysadmin. Without this script, the
    service starts but throws "Login failed for user
    'NT AUTHORITY\NETWORK SERVICE'" on every DB call, the
    /api/v1/health/ready endpoint returns 503, and the login page
    surfaces a 500.

    This script:
      1. Creates a SQL login for NT AUTHORITY\NETWORK SERVICE
         (Windows-authenticated; no password to manage).
      2. Maps it to a database user in EgyptTax.
      3. Adds the user to the db_owner role so it can read,
         write, and execute migrations from the running service
         context.

    Idempotent — re-running is safe (existence checks before each
    statement). Runs as the installing user (admin via UAC), which
    inherits the SQL Express sysadmin grant for
    BUILTIN\Administrators.

    NOTE: assumes the bundled SQL Express install is at
    .\SQLEXPRESS using Windows authentication. Operators using a
    remote SQL Server or a different instance should grant access
    manually via SSMS or sqlcmd before re-running the install.

.PARAMETER SqlInstance
    SQL Server instance to grant permissions on.
    Defaults to .\SQLEXPRESS.

.PARAMETER Database
    Database to grant db_owner on. Defaults to EgyptTax.

.PARAMETER ServiceAccount
    Windows account the EgyptTax service runs under.
    Defaults to NT AUTHORITY\NETWORK SERVICE.
#>
param(
    [Parameter()] [string] $SqlInstance    = ".\SQLEXPRESS",
    [Parameter()] [string] $Database       = "EgyptTax",
    [Parameter()] [string] $ServiceAccount = "NT AUTHORITY\NETWORK SERVICE"
)

$ErrorActionPreference = "Stop"

# Locate sqlcmd.exe — MSI custom actions don't always inherit a
# fully-populated PATH, so a bare `sqlcmd` invocation fails with
# "command not found" and the CA returns non-zero, rolling the
# install back. Search the standard SQL Server install locations
# explicitly.
$sqlcmdExe = $null
$cmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($cmd) { $sqlcmdExe = $cmd.Source }

if (-not $sqlcmdExe) {
    foreach ($base in @(
        'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC',
        'C:\Program Files (x86)\Microsoft SQL Server\Client SDK\ODBC',
        'C:\Program Files\Microsoft SQL Server',
        'C:\Program Files (x86)\Microsoft SQL Server'
    )) {
        if (-not (Test-Path $base)) { continue }
        $found = Get-ChildItem $base -Recurse -Filter 'sqlcmd.exe' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { $sqlcmdExe = $found.FullName; break }
    }
}

if (-not $sqlcmdExe) {
    Write-Error "sqlcmd.exe not found in PATH or standard SQL Server locations."
    exit 4
}

Write-Host "Using sqlcmd at: $sqlcmdExe"
Write-Host "Granting $ServiceAccount db_owner on $SqlInstance/$Database..."

# Build the T-SQL with parameterised principal name. CREATE LOGIN
# doesn't accept variables for the principal name (it must be a
# literal identifier), so we interpolate at PowerShell-string time
# and escape ] in the account by doubling — defensive even though
# the default account contains no brackets.
$escaped = $ServiceAccount -replace ']', ']]'
$sql = @"
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$escaped')
BEGIN
    CREATE LOGIN [$escaped] FROM WINDOWS;
    PRINT 'Created server login [$escaped]';
END
ELSE
BEGIN
    PRINT 'Server login [$escaped] already exists — skipping CREATE LOGIN.';
END

USE [$Database];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$escaped')
BEGIN
    CREATE USER [$escaped] FOR LOGIN [$escaped];
    PRINT 'Created database user [$escaped] in [$Database]';
END
ELSE
BEGIN
    PRINT 'Database user [$escaped] already exists in [$Database].';
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    WHERE u.name = N'$escaped' AND r.name = N'db_owner'
)
BEGIN
    ALTER ROLE db_owner ADD MEMBER [$escaped];
    PRINT 'Added [$escaped] to db_owner role on [$Database]';
END
ELSE
BEGIN
    PRINT '[$escaped] already in db_owner on [$Database].';
END
"@

# Pipe SQL to sqlcmd via stdin so we don't need a temp file. -E
# uses the running user's Windows credentials (sysadmin via the
# BUILTIN\Administrators grant SQL Express creates by default).
$sql | & $sqlcmdExe -E -S $SqlInstance -b -d master
if ($LASTEXITCODE -ne 0)
{
    Write-Error "sqlcmd exited $LASTEXITCODE - could not grant SQL permissions."
    exit $LASTEXITCODE
}

Write-Host "SQL permissions granted successfully."
exit 0
