<#
.SYNOPSIS
  Windows-seitiger Publish von API/Worker (win-x64 self-contained) + UI + ZIP.
  MSI: auf Linux per packaging/build.sh (wixl) oder WiX Toolset mit packaging/wix.
#>
[CmdletBinding()]
param(
    [string]$Version = $(if ($env:GPD_VERSION) { $env:GPD_VERSION } else { '0.1.0' })
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Out = Join-Path $Root 'artifacts'
$Payload = Join-Path $Out 'payload'
$StageApi = Join-Path $Payload 'Api'
$StageWorker = Join-Path $Payload 'Worker'

Write-Host '==> Clean artifacts'
if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
New-Item -ItemType Directory -Force -Path $StageApi, $StageWorker, (Join-Path $Payload 'scripts'), (Join-Path $Payload 'database') | Out-Null

Write-Host '==> Build UI'
Push-Location (Join-Path $Root 'ui')
try {
    if (-not (Test-Path 'node_modules')) { npm ci }
    npm run build
}
finally { Pop-Location }

Write-Host '==> Publish API'
dotnet publish (Join-Path $Root 'src/GroupPolicyDirector.Api/GroupPolicyDirector.Api.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -o $StageApi

$www = Join-Path $StageApi 'wwwroot'
New-Item -ItemType Directory -Force -Path $www | Out-Null
Copy-Item -Path (Join-Path $Root 'ui/dist/*') -Destination $www -Recurse -Force
Copy-Item (Join-Path $Root 'packaging/config/api.appsettings.json') (Join-Path $StageApi 'appsettings.json') -Force

Write-Host '==> Publish Worker'
dotnet publish (Join-Path $Root 'src/GroupPolicyDirector.Worker/GroupPolicyDirector.Worker.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -o $StageWorker
Copy-Item (Join-Path $Root 'packaging/config/worker.appsettings.json') (Join-Path $StageWorker 'appsettings.json') -Force

Copy-Item (Join-Path $Root 'packaging/scripts/*') (Join-Path $Payload 'scripts') -Force
Copy-Item (Join-Path $Root 'database/*') (Join-Path $Payload 'database') -Recurse -Force

Get-ChildItem $StageApi, $StageWorker -Filter 'appsettings.Development.json' -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $StageApi, $StageWorker -Filter '*.pdb' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

$zip = Join-Path $Out "GroupPolicyDirector-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $Payload '*') -DestinationPath $zip
Write-Host "ZIP: $zip"
Write-Host 'MSI: packaging/build.sh (wixl unter Linux) erzeugt GroupPolicyDirector-*-win-x64.msi aus derselben Payload.'
