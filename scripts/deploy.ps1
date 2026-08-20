#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper - dev-deploy orchestration lives in
# cameraunlock-core/powershell/DevDeploy.psm1.

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory=$false, Position=1)]
    [string]$GivenPath,
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\DevDeploy.psm1") -Force
Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\ModDeployment.psm1") -Force
. (Join-Path $scriptDir "common.ps1")

# The dev fast-boot patch (scripts/dev-fastboot.ps1) is additive just like the
# bootstrap patch, so it carries the same corrupt-backup trap: a fast-booted
# assembly captured as .original is restored as "vanilla" forever after.
# Invoke-DevDeployCecil's own guard only knows the head-tracking marker, so the
# second marker is checked here, where the fast-boot tool lives.
$fastBootMarker = 'HeadTracking_FastBoot'
$gamePath = if ($GivenPath) { $GivenPath } else { Resolve-GamePath }
if ($gamePath) {
    $assemblyPath = Join-Path (Get-ManagedPath -GamePath $gamePath) 'Assembly-CSharp.dll'
    $originalPath = "$assemblyPath.original"
    if (Test-Path -LiteralPath $originalPath) {
        if (Test-FileContainsMarker -FilePath $originalPath -Marker $fastBootMarker) {
            throw "Assembly-CSharp.dll.original carries the dev fast-boot patch (corrupt backup). Verify game files via Steam, delete the .original, and re-run."
        }
        if ((Test-Path -LiteralPath $assemblyPath) -and (Test-FileContainsMarker -FilePath $assemblyPath -Marker $fastBootMarker)) {
            Write-Host "Fast-boot patch present - this deploy resets Assembly-CSharp.dll from .original, so re-run 'pixi run dev-fastboot' afterwards." -ForegroundColor Yellow
        }
    } elseif ((Test-Path -LiteralPath $assemblyPath) -and (Test-FileContainsMarker -FilePath $assemblyPath -Marker $fastBootMarker)) {
        throw "Assembly-CSharp.dll carries the dev fast-boot patch and no .original exists, so this deploy would capture it as the pristine backup. Run 'pixi run dev-fastboot-restore' first."
    }
}

$toolsDir = Join-Path $projectRoot "tools"
$cecilPath = & (Join-Path $scriptDir "ensure-cecil.ps1") -ToolsDir $toolsDir

$buildOutput = Join-Path $projectRoot "src\PainscreekHeadTracking\bin\$Configuration\net35"
$result = Invoke-DevDeployCecil `
    -GameId 'painscreek-killings' `
    -GameDisplayName 'Painscreek Killings' `
    -BuildOutputPath $buildOutput `
    -ModDllName 'PainscreekHeadTracking.dll' `
    -ManagedSubfolder 'Painscreek_Data\Managed' `
    -ExtraDlls @('CameraUnlock.Core.dll') `
    -GivenPath $GivenPath `
    -PatchMarker 'HeadTracking_Patched_Painscreek_v2' `
    -Patcher {
        param($assemblyPath)
        Add-Type -Path $cecilPath
        $patcherCode = Get-Content (Join-Path $scriptDir "patcher\BootstrapPatcher.cs") -Raw
        $cp = New-Object System.CodeDom.Compiler.CompilerParameters
        [void]$cp.ReferencedAssemblies.Add($cecilPath)
        [void]$cp.ReferencedAssemblies.Add("System.dll")
        [void]$cp.ReferencedAssemblies.Add("System.Core.dll")
        $cp.CompilerOptions = "/nowarn:1668 /warn:0"
        $cp.TreatWarningsAsErrors = $false
        Add-Type -TypeDefinition $patcherCode -CompilerParameters $cp
        if (-not [BootstrapPatcher]::PatchAssembly($assemblyPath)) {
            throw "BootstrapPatcher::PatchAssembly returned false"
        }
    }

Write-DeploymentSuccess `
    -ModName "Head Tracking mod" `
    -DeployPath $result.DeployedDllPath `
    -Controls @(
        "End       - Toggle head tracking on/off",
        "Page Up   - Cycle tracking mode (full / rotation-only / position-only)",
        "Page Down - Toggle yaw mode (world / local)",
        "",
        "No nav cluster? Chords: Ctrl+Shift+ Y=Toggle G=Mode H=Yaw"
    )