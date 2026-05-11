<#
.SYNOPSIS
    Writes appsettings.Production.json from MSI-supplied properties.

.DESCRIPTION
    Bridges the operator-supplied MSI properties (SQL_CONNECTION,
    AUDIT_CHECKPOINT_MODE, ATTACHMENTS_ROOT, NTP_SERVER) into the
    runtime configuration the EgyptTax web service + seed CLI both
    consume. ASP.NET Core's chained config sources read
    appsettings.Production.json AFTER appsettings.json, so values
    here override the publish-time defaults shipped with the binary.

    Without this step, the seed CLI exits with code 4
    "No connection string found" and the MSI rolls back.

.PARAMETER SqlConnection
    SQL Server connection string. Required.

.PARAMETER AuditCheckpointMode
    "Sql" or "FileSystem". Defaults to "Sql".

.PARAMETER AttachmentsRoot
    Directory for receipt uploads. Defaults to
    C:\ProgramData\EgyptTax\attachments.

.PARAMETER NtpServer
    NTP server for clock-drift defense. Defaults to
    time.windows.com.

.PARAMETER BindUrl
    Kestrel bind URL the EgyptTax Windows service listens on.
    Defaults to http://+:8088 to match the MSI's firewall rule.
    Operators wanting HTTPS instead should pass
    https://+:443 + run setup-https.ps1 to provision the cert.

.NOTES
    InstallFolder is derived from $PSScriptRoot rather than a
    parameter. This sidesteps the MSI INSTALLFOLDER trailing-
    backslash trap: [INSTALLFOLDER] always ends with `\`, so
    -InstallFolder "[INSTALLFOLDER]" becomes
    -InstallFolder "C:\Program Files\EgyptTax\" — and the
    Windows command-line parser interprets the trailing \"
    as an escaped quote, mangling the rest of argv and leaving
    the -SqlConnection parameter empty. The script would then
    hang prompting for the missing Mandatory parameter. Using
    $PSScriptRoot avoids the issue entirely — apply-config.ps1
    sits in the install folder by definition, so $PSScriptRoot
    IS the install folder.
#>
param(
    [Parameter(Mandatory)] [string] $SqlConnection,
    [Parameter()] [string] $AuditCheckpointMode = "Sql",
    [Parameter()] [string] $AttachmentsRoot = "C:\ProgramData\EgyptTax\attachments",
    [Parameter()] [string] $NtpServer = "time.windows.com",
    [Parameter()] [string] $BindUrl = "http://+:8088"
)

$ErrorActionPreference = "Stop"

$path = Join-Path $PSScriptRoot "appsettings.Production.json"

$config = [ordered]@{
    ConnectionStrings = [ordered]@{
        EgyptTax = $SqlConnection
    }
    AuditCheckpoint = [ordered]@{
        Mode = $AuditCheckpointMode
        FileSystemRoot = "C:\ProgramData\EgyptTax\audit-checkpoints"
    }
    Attachments = [ordered]@{
        RootDirectory = $AttachmentsRoot
    }
    Ntp = [ordered]@{
        Server = $NtpServer
    }
    InspectionBundles = [ordered]@{
        RootDirectory = "C:\ProgramData\EgyptTax\inspection-bundles"
    }
    # ASP.NET Core reads "Urls" from configuration to know which
    # endpoints to bind. Equivalent to passing --urls or setting
    # ASPNETCORE_URLS at process start, but config-driven so the
    # Windows service registration doesn't need an env-var dance.
    Urls = $BindUrl
}

$json = $config | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host "Wrote $path"
exit 0
