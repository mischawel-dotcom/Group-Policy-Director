#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Registriert Group Policy Director API und Worker als Windows-Dienste.

.DESCRIPTION
  Erwartet die installierte Payload unter -InstallRoot (Standard:
  %ProgramFiles%\Group Policy Director). Konfiguration in Api\appsettings.json
  und Worker\appsettings.json vorher anpassen (Connection String, SMTP, Auth,
  ForceDryRun).

.PARAMETER InstallRoot
  Installationsverzeichnis.

.PARAMETER SkipStart
  Dienste nur anlegen, nicht starten.
#>
[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path ${env:ProgramFiles} 'Group Policy Director'),
    [switch]$SkipStart
)

$ErrorActionPreference = 'Stop'

$apiExe = Join-Path $InstallRoot 'Api\GroupPolicyDirector.Api.exe'
$workerExe = Join-Path $InstallRoot 'Worker\GroupPolicyDirector.Worker.exe'

foreach ($path in @($apiExe, $workerExe)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Datei nicht gefunden: $path — zuerst MSI/ZIP installieren bzw. Publish ausführen."
    }
}

function Ensure-Service {
    param(
        [string]$Name,
        [string]$DisplayName,
        [string]$Description,
        [string]$BinaryPath
    )

    $existing = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "Dienst $Name existiert bereits — wird neu konfiguriert."
        if ($existing.Status -eq 'Running') {
            Stop-Service -Name $Name -Force
        }
        sc.exe config $Name binPath= "`"$BinaryPath`"" start= auto | Out-Null
    }
    else {
        New-Service -Name $Name `
            -BinaryPathName "`"$BinaryPath`"" `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic | Out-Null
        Write-Host "Dienst $Name angelegt."
    }
}

Ensure-Service -Name 'GroupPolicyDirectorApi' `
    -DisplayName 'Group Policy Director API' `
    -Description 'GPD Web-API und UI (Kestrel).' `
    -BinaryPath $apiExe

Ensure-Service -Name 'GroupPolicyDirectorWorker' `
    -DisplayName 'Group Policy Director Worker' `
    -Description 'GPD Hintergrunddienst (OOB-Scan).' `
    -BinaryPath $workerExe

if (-not $SkipStart) {
    Start-Service -Name 'GroupPolicyDirectorApi'
    Start-Service -Name 'GroupPolicyDirectorWorker'
    Write-Host 'Dienste gestartet.'
}

Write-Host @"

Fertig.
- UI/API: siehe Urls in Api\appsettings.json (Standard http://localhost:5080)
- ForceDryRun ist in der Packaging-Vorlage true — Live-AD erst nach bewusster Umstellung.
- Auth:Negotiate und SMTP/ConnectionStrings in den appsettings.json setzen.
"@
