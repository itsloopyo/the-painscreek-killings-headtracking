#Requires -Version 5.1
<#
.SYNOPSIS
    Restore Painscreek's Assembly-CSharp.dll from the fast-boot backup.
#>
param(
    [Parameter(Mandatory=$false)]
    [string]$GamePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'common.ps1')

if (-not $GamePath) {
    $GamePath = Resolve-GamePath
    if (-not $GamePath) {
        Show-GameNotFoundError
        exit 1
    }
}

$managed = Get-ManagedPath -GamePath $GamePath
$asmPath = Join-Path $managed 'Assembly-CSharp.dll'
$backupPath = "$asmPath.fastboot-backup"

if (-not (Test-Path $backupPath)) {
    Write-Host "  No fast-boot backup found at: $backupPath" -ForegroundColor Yellow
    Write-Host "  Nothing to restore." -ForegroundColor Yellow
    exit 0
}

Copy-Item -Path $backupPath -Destination $asmPath -Force
Remove-Item -Path $backupPath -Force
Write-Host "  Restored original Assembly-CSharp.dll. Fast-boot disabled." -ForegroundColor Green
Write-Host "  Note: re-run 'pixi run install' to re-apply the head-tracking bootstrap patch." -ForegroundColor DarkGray
