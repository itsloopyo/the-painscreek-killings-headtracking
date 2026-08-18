#!/usr/bin/env pwsh
#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

# Import common functions
. (Join-Path $scriptDir "common.ps1")
Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ModDeployment.psm1") -Force

# Find game installation
$gamePath = Resolve-GamePath

if (-not $gamePath) {
    Show-GameNotFoundError
    exit 1
}

Write-Host "Found game installation at: $gamePath" -ForegroundColor Green

$managedPath = Get-ManagedPath -GamePath $gamePath
$assemblyCSharpPath = Join-Path $managedPath "Assembly-CSharp.dll"
$assemblyCSharpBackup = Join-Path $managedPath "Assembly-CSharp.dll.original"
$fastBootBackup = "$assemblyCSharpPath.fastboot-backup"

# Both patches are additive, so a backup taken from an already-patched assembly
# is a modded file wearing a vanilla name. Restoring one leaves the game patched
# while reporting success, which is how a broken assembly outlives an uninstall.
$patchMarkers = @('HeadTracking_Patched_Painscreek', 'HeadTracking_FastBoot')

if (Test-Path $assemblyCSharpBackup) {
    foreach ($marker in $patchMarkers) {
        if (Test-FileContainsMarker -FilePath $assemblyCSharpBackup -Marker $marker) {
            throw "Assembly-CSharp.dll.original carries the $marker patch - corrupt backup, not restoring. Delete it, then run Steam 'Verify integrity of game files' to restore a clean assembly."
        }
    }
    Copy-Item -Path $assemblyCSharpBackup -Destination $assemblyCSharpPath -Force
    Remove-Item -Path $assemblyCSharpBackup -Force
    Write-Host "Restored original Assembly-CSharp.dll" -ForegroundColor Green
} else {
    foreach ($marker in $patchMarkers) {
        if (Test-FileContainsMarker -FilePath $assemblyCSharpPath -Marker $marker) {
            throw "Assembly-CSharp.dll carries the $marker patch but no .original backup exists. Run Steam 'Verify integrity of game files' to restore a clean assembly."
        }
    }
}

# A leftover fast-boot backup is a patched assembly that dev-fastboot-restore
# would later copy back over the vanilla one.
if (Test-Path $fastBootBackup) {
    Remove-Item -Path $fastBootBackup -Force
    Write-Host "Removed dev fast-boot backup" -ForegroundColor Gray
}

# Remove mod files
$filesToRemove = @(
    "PainscreekHeadTracking.dll",
    "PainscreekHeadTracking.pdb",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll",
    "HeadTracking.log",
    "HeadTracking_BOOT.log"
)

foreach ($file in $filesToRemove) {
    $filePath = Join-Path $managedPath $file
    if (Test-Path $filePath) {
        Remove-Item $filePath -Force
        Write-Host "Removed: $file" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Uninstall complete!" -ForegroundColor Green
