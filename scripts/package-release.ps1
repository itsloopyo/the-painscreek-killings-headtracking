#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Packages the mod for release distribution.
.DESCRIPTION
    Creates the release ZIP:
    - PainscreekHeadTracking-v{version}-installer.zip (GitHub Release: install/uninstall scripts + mod/ + docs)

    Painscreek is not published on Nexus Mods, so no nexus artifact is produced.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectRoot "src\PainscreekHeadTracking\PainscreekHeadTracking.csproj"

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force
$buildOutput = Join-Path $projectRoot "src\PainscreekHeadTracking\bin\Release\net35"
$vendorCecilDir = Join-Path $projectRoot "vendor\mono-cecil"
$releaseDir = Join-Path $projectRoot "release"

Write-Host "=== Painscreek Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""

$version = Get-CsprojVersion $csprojPath
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

$modDlls = @("PainscreekHeadTracking.dll", "CameraUnlock.Core.dll")

# Validate build output exists
foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutput $dll
    if (-not (Test-Path $dllPath)) {
        Write-Host "ERROR: Required DLL not found: $dll. Run 'pixi run build' first." -ForegroundColor Red
        exit 1
    }
}

# Validate install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptDir $script
    if (-not (Test-Path $scriptPath)) {
        Write-Host "ERROR: Required script not found: $scriptPath" -ForegroundColor Red
        exit 1
    }
}

# Validate patcher source
$patcherSource = Join-Path $scriptDir "patcher\BootstrapPatcher.cs"
if (-not (Test-Path $patcherSource)) {
    Write-Host "ERROR: Patcher source not found: $patcherSource" -ForegroundColor Red
    exit 1
}
$patcherMain = Join-Path $scriptDir "patcher\PatcherMain.cs"
if (-not (Test-Path $patcherMain)) {
    Write-Host "ERROR: Patcher wrapper not found: $patcherMain" -ForegroundColor Red
    exit 1
}

# Create release directory
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Extract Mono.Cecil.dll from the vendored .nupkg. install.cmd compiles
# BootstrapPatcher.cs at install time and needs the cecil DLL deployed to
# Managed/ alongside the mod (it's listed in MOD_DLLS). Packaging is offline:
# the vendored zip is the source of truth, refreshed manually via
# `pixi run update-deps` and committed.
$cecilNupkg = Get-ChildItem -Path $vendorCecilDir -Filter 'Mono.Cecil.*.nupkg' -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $cecilNupkg) {
    Write-Host "ERROR: Vendored Mono.Cecil .nupkg not found in $vendorCecilDir." -ForegroundColor Red
    Write-Host "Run 'pixi run update-deps' to refresh, then commit." -ForegroundColor Yellow
    exit 1
}

$cecilExtractDir = Join-Path $releaseDir "_cecil-extract"
if (Test-Path $cecilExtractDir) { Remove-Item -Recurse -Force $cecilExtractDir }
New-Item -ItemType Directory -Path $cecilExtractDir -Force | Out-Null

$cecilNupkgZip = Join-Path $cecilExtractDir ($cecilNupkg.BaseName + '.zip')
Copy-Item -Path $cecilNupkg.FullName -Destination $cecilNupkgZip -Force
Expand-Archive -Path $cecilNupkgZip -DestinationPath $cecilExtractDir -Force

$cecilPath = Join-Path $cecilExtractDir 'lib\net40\Mono.Cecil.dll'
if (-not (Test-Path $cecilPath)) {
    Write-Host "ERROR: Mono.Cecil.dll not found in vendored .nupkg ($($cecilNupkg.Name))." -ForegroundColor Red
    exit 1
}
Write-Host "  Extracted Mono.Cecil.dll from vendor/mono-cecil/$($cecilNupkg.Name)" -ForegroundColor Gray

# --- GitHub Release ZIP (with installer) ---

Write-Host "--- GitHub Release ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $releaseDir "staging-github"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

# Copy install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# Canonical launcher manifest. Stamp the real release version and drop it at the
# installer ZIP root. Lopari uses delivery_mode manifest for native deploy;
# install.cmd remains in the ZIP for users running the package manually.
$manifestSource = Join-Path $projectRoot "launcher-manifest.json"
if (-not (Test-Path $manifestSource)) {
    Write-Host "ERROR: launcher-manifest.json not found at repo root: $manifestSource" -ForegroundColor Red
    exit 1
}
$manifestJson = Get-Content $manifestSource -Raw | ConvertFrom-Json
$manifestJson.mod_info.version = $version
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText(
    (Join-Path $ghStagingDir "launcher-manifest.json"),
    ($manifestJson | ConvertTo-Json -Depth 10),
    $utf8NoBom
)
Write-Host "  launcher-manifest.json (v$version)" -ForegroundColor Green

# Copy mod files to mod subfolder. install.cmd reads from %SCRIPT_DIR%mod.
$modDestDir = Join-Path $ghStagingDir "mod"
New-Item -ItemType Directory -Path $modDestDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutput $dll) -Destination $modDestDir -Force
    Write-Host "  mod/$dll" -ForegroundColor Green
}

Copy-Item $cecilPath -Destination $modDestDir -Force
Write-Host "  mod/Mono.Cecil.dll" -ForegroundColor Green

Copy-Item $patcherSource -Destination $modDestDir -Force
Write-Host "  mod/BootstrapPatcher.cs" -ForegroundColor Green

$nativeToolsDir = Join-Path $ghStagingDir "tools"
New-Item -ItemType Directory -Path $nativeToolsDir -Force | Out-Null
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    Write-Host "ERROR: csc.exe not found at $csc" -ForegroundColor Red
    exit 1
}
$patcherExe = Join-Path $nativeToolsDir "BootstrapPatcher.exe"
& $csc /nologo /target:exe /out:$patcherExe /reference:$cecilPath $patcherSource $patcherMain
if ($LASTEXITCODE -ne 0) {
    throw "Failed to compile BootstrapPatcher.exe"
}
Copy-Item $cecilPath -Destination $nativeToolsDir -Force
Write-Host "  tools/BootstrapPatcher.exe" -ForegroundColor Green
Write-Host "  tools/Mono.Cecil.dll" -ForegroundColor Green

# Copy documentation
$docFiles = @("README.md", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectRoot $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $ghStagingDir -Force
        Write-Host "  $doc" -ForegroundColor Green
    }
}

# Ship the vendored loader source (committed .nupkg + LICENSE + README) for
# transparency and license attribution. Cecil install.cmd ships the
# pre-extracted DLL in mod/, so this directory is informational on this mod.
$vendorStagingDir = Join-Path $ghStagingDir "vendor\mono-cecil"
New-Item -ItemType Directory -Path $vendorStagingDir -Force | Out-Null
foreach ($file in @($cecilNupkg.Name, 'LICENSE', 'README.md')) {
    $src = Join-Path $vendorCecilDir $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $vendorStagingDir -Force
        Write-Host "  vendor/mono-cecil/$file" -ForegroundColor Green
    }
}

Copy-SharedBundle -StagingDir $ghStagingDir

$ghZipName = "PainscreekHeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $releaseDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating GitHub ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

if (Test-Path $cecilExtractDir) { Remove-Item -Recurse -Force $cecilExtractDir }

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host ("GitHub Release: $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

Write-Output $ghZipPath
