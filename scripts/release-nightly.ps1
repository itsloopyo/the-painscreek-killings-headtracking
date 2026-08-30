[CmdletBinding()]
param(
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

Import-Module (Join-Path $ProjectRoot 'cameraunlock-core\powershell\NightlyRelease.psm1') -Force

$csprojPath = Join-Path $ProjectRoot 'src\PainscreekHeadTracking\PainscreekHeadTracking.csproj'
$versionMatch = Select-String -Path $csprojPath -Pattern '<Version>([^<]+)</Version>'
if (-not $versionMatch) {
    throw "Could not extract version from $csprojPath"
}
$version = $versionMatch.Matches[0].Groups[1].Value

# scripts/package-release.ps1 builds the installer ZIP only.
Publish-NightlyBuild `
    -ModId 'painscreek-killings' `
    -ModName 'PainscreekHeadTracking' `
    -Version $version `
    -ProjectRoot $ProjectRoot `
    -BuildCommand 'pixi run build' `
    -NoNexusZip `
    -AllowDirty:$AllowDirty
