#!/usr/bin/env pwsh
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "=== Painscreek Killings Head Tracking - Release Validation ===" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

# LICENSE, THIRD-PARTY-NOTICES.md and the cameraunlock-core licence are the
# copyright notices MIT requires to accompany the binaries in the release ZIP.
# A release ZIP shipped without them is a licence violation, not a packaging nit.
$requiredDocs = @(
    "README.md",
    "LICENSE",
    "THIRD-PARTY-NOTICES.md",
    "cameraunlock-core\LICENSE"
)
foreach ($file in $requiredDocs) {
    Write-Host "Checking $file..." -ForegroundColor Gray
    if (Test-Path (Join-Path $projectRoot $file)) {
        Write-Host "  $file exists" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: $file not found" -ForegroundColor Red
        $allPassed = $false
    }
}

foreach ($file in @("scripts\install.cmd", "scripts\uninstall.cmd", "scripts\patcher\BootstrapPatcher.cs")) {
    Write-Host "Checking $file..." -ForegroundColor Gray
    if (Test-Path (Join-Path $projectRoot $file)) {
        Write-Host "  $file exists" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: $file not found" -ForegroundColor Red
        $allPassed = $false
    }
}

Write-Host "Checking build output..." -ForegroundColor Gray
$dllPath = Join-Path $projectRoot "src\PainscreekHeadTracking\bin\Release\net35\PainscreekHeadTracking.dll"
if (Test-Path $dllPath) {
    $dllInfo = Get-Item $dllPath
    Write-Host "  PainscreekHeadTracking.dll exists ($($dllInfo.Length) bytes)" -ForegroundColor Green
} else {
    Write-Host "  WARNING: PainscreekHeadTracking.dll not found" -ForegroundColor Yellow
    $allPassed = $false
}

# The packaged ZIP is what users actually receive, so verify the notices are
# inside it rather than trusting that the packager copied them.
$latestZip = Get-ChildItem -Path (Join-Path $projectRoot "release") -Filter "*-installer.zip" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($latestZip) {
    Write-Host "Checking licence files inside $($latestZip.Name)..." -ForegroundColor Gray
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($latestZip.FullName)
    try {
        $entries = $zip.Entries | ForEach-Object { $_.FullName.Replace([char]92, [char]47) }
        $requiredInZip = @(
            "LICENSE",
            "THIRD-PARTY-NOTICES.md",
            "licenses/cameraunlock-core-LICENSE.txt",
            "vendor/mono-cecil/LICENSE"
        )
        foreach ($entry in $requiredInZip) {
            if ($entries -contains $entry) {
                Write-Host "  $entry present" -ForegroundColor Green
            } else {
                Write-Host "  ERROR: $entry missing from the release ZIP" -ForegroundColor Red
                $allPassed = $false
            }
        }
    } finally {
        $zip.Dispose()
    }
} else {
    Write-Host "  WARNING: no installer ZIP in release/ - run 'pixi run package' to validate its contents" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "===============================" -ForegroundColor Cyan

if ($allPassed) {
    Write-Host "All validation checks passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some validation checks failed." -ForegroundColor Yellow
    exit 1
}
