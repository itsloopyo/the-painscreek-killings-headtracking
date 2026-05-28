#!/usr/bin/env pwsh
#Requires -Version 5.1
# Common functions for Painscreek Killings scripts
# Provides wrappers around shared cameraunlock-core modules

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$sharedRoot = Join-Path $projectRoot "cameraunlock-core\powershell"

# Import shared modules
Import-Module (Join-Path $sharedRoot "GamePathDetection.psm1") -Force

# Game configuration
$Script:GameId = 'painscreek-killings'
$Script:Config = Get-GameConfig -GameId $Script:GameId

function Resolve-GamePath {
    return Find-GamePath -GameId $Script:GameId
}

function Show-GameNotFoundError {
    # Wrapper that calls the shared module's Write-GameNotFoundError with our game's config
    GamePathDetection\Write-GameNotFoundError `
        -GameName 'The Painscreek Killings' `
        -EnvVar $Script:Config.EnvVar `
        -SteamFolder $Script:Config.SteamFolder
}

function Get-ManagedPath {
    param(
        [Parameter(Mandatory=$true)]
        [string]$GamePath
    )
    return Join-Path $GamePath "$($Script:Config.DataFolder)\Managed"
}
