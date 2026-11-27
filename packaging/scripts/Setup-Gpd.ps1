#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Klartext-Setup: kopiert die Publish-Payload und registriert Windows-Dienste.

.DESCRIPTION
  Alternative zum MSI, wenn kein msiexec gewünscht ist. Erwartet neben diesem
  Skript die Ordner Api\ und Worker\ (wie von packaging/build.* erzeugt).

.PARAMETER SourceRoot
  Quelle mit Api\ und Worker\ (Standard: Elternverzeichnis dieses Skripts).

.PARAMETER InstallRoot
  Zielverzeichnis.
#>
[CmdletBinding()]
param(
    [string]$SourceRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$InstallRoot = (Join-Path ${env:ProgramFiles} 'Group Policy Director')
)

$ErrorActionPreference = 'Stop'

$apiSrc = Join-Path $SourceRoot 'Api'
$workerSrc = Join-Path $SourceRoot 'Worker'
if (-not (Test-Path -LiteralPath (Join-Path $apiSrc 'GroupPolicyDirector.Api.exe'))) {
    throw "Api-Payload fehlt unter $apiSrc"
}
if (-not (Test-Path -LiteralPath (Join-Path $workerSrc 'GroupPolicyDirector.Worker.exe'))) {
    throw "Worker-Payload fehlt unter $workerSrc"
}

New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
Write-Host "Kopiere nach $InstallRoot ..."
Copy-Item -Path $apiSrc -Destination (Join-Path $InstallRoot 'Api') -Recurse -Force
Copy-Item -Path $workerSrc -Destination (Join-Path $InstallRoot 'Worker') -Recurse -Force

$scriptsDir = Join-Path $InstallRoot 'scripts'
New-Item -ItemType Directory -Force -Path $scriptsDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot '*.ps1') -Destination $scriptsDir -Force

if (Test-Path -LiteralPath (Join-Path $SourceRoot 'database')) {
    Copy-Item -Path (Join-Path $SourceRoot 'database') -Destination (Join-Path $InstallRoot 'database') -Recurse -Force
}

& (Join-Path $PSScriptRoot 'Install-Gpd.ps1') -InstallRoot $InstallRoot
