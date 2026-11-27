#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Stoppt und entfernt die Group-Policy-Director-Windows-Dienste.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

foreach ($name in @('GroupPolicyDirectorApi', 'GroupPolicyDirectorWorker')) {
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Host "Dienst $name nicht vorhanden."
        continue
    }
    if ($svc.Status -eq 'Running') {
        Stop-Service -Name $name -Force
    }
    sc.exe delete $name | Out-Null
    Write-Host "Dienst $name entfernt."
}
