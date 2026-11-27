<#
.SYNOPSIS
  Windows publish of API/Worker (win-x64 self-contained) + UI + ZIP + MSI.
  MSI requires WiX Toolset CLI v5+ (`wix`); Linux/cloud MSI uses packaging/build.sh (wixl).
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

function Build-Msi {
    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if (-not $wix) {
        Write-Host '!! wix (WiX Toolset CLI) not found — skipping MSI (ZIP + Setup.ps1 remain).'
        Write-Host '   Install: winget install --id WiXToolset.WiXCLI -e'
        Write-Host '   Then:    wix eula accept wix7'
        return
    }

    Write-Host '==> Build MSI with WiX CLI'
    $msi = Join-Path $Out "GroupPolicyDirector-$Version-win-x64.msi"
    $product = Join-Path $Root 'packaging/wix/Product.windows.wxs'
    if (-not (Test-Path $product)) {
        throw "Missing WiX product source: $product"
    }

    & $wix.Source build `
        -arch x64 `
        -o $msi `
        -b "PayloadSource=$Payload" `
        $product
    if ($LASTEXITCODE -ne 0) {
        throw "wix build failed with exit code $LASTEXITCODE"
    }
    if (-not (Test-Path $msi)) {
        throw "MSI was not produced at $msi"
    }

    $sizeMb = [math]::Round((Get-Item $msi).Length / 1MB, 1)
    Write-Host "MSI: $msi ($sizeMb MB)"
}

Build-Msi
Write-Host '==> Done'
Get-ChildItem $Out -File | Format-Table Name, Length -AutoSize
Write-Host "Payload: $Payload"
