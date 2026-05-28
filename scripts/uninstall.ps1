#!/usr/bin/env pwsh
#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Import common functions
. (Join-Path $scriptDir "common.ps1")

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

# Restore original Assembly-CSharp.dll if backup exists
if (Test-Path $assemblyCSharpBackup) {
    Copy-Item -Path $assemblyCSharpBackup -Destination $assemblyCSharpPath -Force
    Write-Host "Restored original Assembly-CSharp.dll" -ForegroundColor Green
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
