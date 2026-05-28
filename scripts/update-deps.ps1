#!/usr/bin/env pwsh
#Requires -Version 5.1
# Bump vendored Mono.Cecil package to the latest pinned version. Manual: dev
# runs this when they want a fresh upstream bump, then commits the result.
# CI never refreshes. See ~/.claude/CLAUDE.md "Vendoring Third-Party Dependencies".
#
# Mono.Cecil has no GitHub release assets (jbevain/cecil only publishes source
# tags), so we pin the NuGet package directly via DirectUrl. Bumping the
# version is a deliberate edit to $cecilVersion below.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

$module = Join-Path $projectDir 'cameraunlock-core/powershell/ModLoaderSetup.psm1'
if (-not (Test-Path $module)) {
    throw "ModLoaderSetup.psm1 not found at $module. Run 'pixi run sync' to update the cameraunlock-core submodule."
}
Import-Module $module -Force

$cecilVersion = '0.11.5'
$out          = Join-Path $projectDir 'vendor/mono-cecil'
$outputFile   = "Mono.Cecil.$cecilVersion.nupkg"

Refresh-VendoredLoader `
    -Name 'mono-cecil' `
    -OutputDir $out `
    -OutputFileName $outputFile `
    -DirectUrl "https://www.nuget.org/api/v2/package/Mono.Cecil/$cecilVersion" `
    -LicenseUrl 'https://raw.githubusercontent.com/jbevain/cecil/master/LICENSE.txt' | Out-Null

Write-Host ""
Write-Host "vendor/mono-cecil refreshed (Mono.Cecil $cecilVersion). Review and commit." -ForegroundColor Green
