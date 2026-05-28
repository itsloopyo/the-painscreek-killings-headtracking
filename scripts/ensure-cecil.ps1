#Requires -Version 5.1
<#
.SYNOPSIS
    Ensures Mono.Cecil is available in the tools directory.
.DESCRIPTION
    Extracts Mono.Cecil.dll from the vendored .nupkg under vendor/mono-cecil/.
    The vendored copy is the install-time source of truth (see AGENTS.md vendoring
    doctrine); this script never reaches out to the network. To bump cecil,
    run `pixi run update-deps` and commit the refreshed vendor tree.
    Returns the path to Mono.Cecil.dll.
.PARAMETER ToolsDir
    Path to the tools directory where Mono.Cecil.dll should be placed.
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$ToolsDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$cecilPath = Join-Path $ToolsDir "Mono.Cecil.dll"

if (Test-Path $cecilPath) {
    return $cecilPath
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$vendorDir = Join-Path $projectRoot 'vendor\mono-cecil'

$vendorNupkg = Get-ChildItem -Path $vendorDir -Filter 'Mono.Cecil.*.nupkg' -File -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $vendorNupkg) {
    throw "Vendored Mono.Cecil .nupkg not found in $vendorDir. Run 'pixi run update-deps' to refresh, then commit."
}

if (-not (Test-Path $ToolsDir)) {
    New-Item -ItemType Directory -Path $ToolsDir -Force | Out-Null
}

$extractDir = Join-Path $ToolsDir '_cecil-extract'
if (Test-Path $extractDir) { Remove-Item -Recurse -Force $extractDir }
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

try {
    $zipCopy = Join-Path $extractDir ($vendorNupkg.BaseName + '.zip')
    Copy-Item -Path $vendorNupkg.FullName -Destination $zipCopy -Force
    Expand-Archive -Path $zipCopy -DestinationPath $extractDir -Force

    $extractedDll = Join-Path $extractDir 'lib\net40\Mono.Cecil.dll'
    if (-not (Test-Path $extractedDll)) {
        throw "Mono.Cecil.dll not found inside vendored package $($vendorNupkg.Name) at lib\net40\."
    }

    Copy-Item -Path $extractedDll -Destination $cecilPath -Force
    Write-Host "  Extracted Mono.Cecil from vendor/mono-cecil/$($vendorNupkg.Name)" -ForegroundColor Green
} finally {
    if (Test-Path $extractDir) { Remove-Item -Recurse -Force $extractDir }
}

return $cecilPath
