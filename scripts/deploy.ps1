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
    -RecenterKey "Home" `
    -ToggleKey "End"