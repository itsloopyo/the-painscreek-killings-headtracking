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
$projectRoot = Split-Path -Parent $scriptDir
. (Join-Path $scriptDir 'common.ps1')
Import-Module (Join-Path $projectRoot 'cameraunlock-core\powershell\ModDeployment.psm1') -Force

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

$fastBootMarker = 'HeadTracking_FastBoot'

if (-not (Test-Path $backupPath)) {
    Write-Host "  No fast-boot backup found at: $backupPath" -ForegroundColor Yellow
    Write-Host "  Nothing to restore." -ForegroundColor Yellow
    exit 0
}

# The backup is only ever the right thing to restore while the live assembly
# actually carries the fast-boot patch. Restoring it over an unpatched assembly
# would put back whatever `pixi run install` deployed at capture time, silently
# replacing a newer bootstrap patch with a stale one.
if (-not (Test-FileContainsMarker -FilePath $asmPath -Marker $fastBootMarker)) {
    Write-Host "  Assembly-CSharp.dll is not fast-booted - nothing to restore." -ForegroundColor Yellow
    Remove-Item -Path $backupPath -Force
    Write-Host "  Removed the redundant backup: $backupPath" -ForegroundColor DarkGray
    exit 0
}

if (Test-FileContainsMarker -FilePath $backupPath -Marker $fastBootMarker) {
    Write-Error "$backupPath is itself fast-booted (corrupt backup); restoring it would leave the patch in place. Delete it, verify game files via Steam, then re-run 'pixi run install'."
    exit 1
}

Copy-Item -Path $backupPath -Destination $asmPath -Force
Remove-Item -Path $backupPath -Force
Write-Host "  Restored pre-fast-boot Assembly-CSharp.dll. Fast-boot disabled." -ForegroundColor Green
Write-Host "  The assembly is back to whatever 'pixi run install' last deployed." -ForegroundColor DarkGray
