#!/usr/bin/env pwsh
#Requires -Version 5.1
# Creates Unity stub assemblies for CI builds.
# Called from GitHub Actions workflows (build.yml and release.yml).

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsPath = Join-Path $projectRoot "src/PainscreekHeadTracking/libs"

if (Test-Path (Join-Path $libsPath "UnityEngine.dll")) {
    Write-Host "UnityEngine.dll already present in libs/, skipping stub generation" -ForegroundColor Yellow
    exit 0
}

Write-Host "Creating Unity stub assemblies for CI build..." -ForegroundColor Cyan

# Build UnityEngine.dll with all types from checked-in UnityStubs.cs
$projContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net35</TargetFramework>
    <LangVersion>11</LangVersion>
    <AssemblyName>UnityEngine</AssemblyName>
    <NoWarn>CS0169;CS0649;CS0067;CS0660;CS0661</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="UnityStubs.cs" />
  </ItemGroup>
</Project>
"@
$projPath = Join-Path $libsPath "Stub_UnityEngine.csproj"
$projContent | Out-File -FilePath $projPath -Encoding utf8

dotnet build $projPath -c Release -o $libsPath --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "::error::Failed to build UnityEngine stub"
    exit 1
}
Write-Host "  Created UnityEngine.dll" -ForegroundColor Green
Remove-Item $projPath -ErrorAction SilentlyContinue

# Build empty stubs for other Unity modules
$emptySource = "// Empty stub assembly"
$emptySourcePath = Join-Path $libsPath "EmptyStub.cs"
$emptySource | Out-File -FilePath $emptySourcePath -Encoding utf8

$emptyModules = @(
    "UnityEngine.CoreModule",
    "UnityEngine.IMGUIModule",
    "UnityEngine.PhysicsModule",
    "UnityEngine.UIModule",
    "UnityEngine.TextRenderingModule",
    "UnityEngine.InputLegacyModule",
    "UnityEngine.UI"
)

foreach ($moduleName in $emptyModules) {
    $emptyProjContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net35</TargetFramework>
    <AssemblyName>$moduleName</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="EmptyStub.cs" />
  </ItemGroup>
</Project>
"@
    $emptyProjPath = Join-Path $libsPath "Stub_$moduleName.csproj"
    $emptyProjContent | Out-File -FilePath $emptyProjPath -Encoding utf8

    dotnet build $emptyProjPath -c Release -o $libsPath --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error::Failed to build $moduleName stub"
        exit 1
    }
    Write-Host "  Created $moduleName.dll" -ForegroundColor Green
    Remove-Item $emptyProjPath -ErrorAction SilentlyContinue
}

# Cleanup temp files
Remove-Item $emptySourcePath -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath "*.deps.json") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath "*.pdb") -Force -ErrorAction SilentlyContinue
Write-Host "Unity stub assemblies created" -ForegroundColor Green
