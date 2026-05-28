#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Reverts The Painscreek Killings to vanilla (unmodded) state.
.DESCRIPTION
    Removes all mod files and restores original Assembly-CSharp.dll.
    For this game, vanilla is the same as uninstall since we don't install any mod loaders.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "=== The Painscreek Killings - Revert to Vanilla ===" -ForegroundColor Magenta
Write-Host ""

# Run uninstall - this restores Assembly-CSharp.dll and removes mod files
& (Join-Path $scriptDir "uninstall.ps1")
