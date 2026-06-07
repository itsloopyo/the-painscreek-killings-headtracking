#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for The Painscreek Killings Head Tracking mod.

.DESCRIPTION
    This script:
    1. Updates version in csproj
    2. Commits the version change
    3. Creates and pushes a git tag to trigger CI release

.PARAMETER Version
    The version to release (e.g., "1.0.0", "1.2.3")

.EXAMPLE
    pixi run release 1.0.0

.NOTES
    Run via: pixi run release <version>
#>
param(
    [Parameter(Position=0)]
    [string]$Version = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\PainscreekHeadTracking\PainscreekHeadTracking.csproj"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host "=== Painscreek Killings Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

# If no version provided, show current and exit
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <major|minor|patch|nightly|X.Y.Z>" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release patch" -ForegroundColor White
    exit 0
}

if ($Version -eq 'nightly') {
    & (Join-Path $scriptDir 'release-nightly.ps1')
    exit $LASTEXITCODE
}

# Resolve major/minor/patch into a concrete version (or accept literal X.Y.Z)
try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

# Check if we're on main branch
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host $status -ForegroundColor Gray
    Write-Host "Please commit or stash changes before releasing" -ForegroundColor Yellow
    exit 1
}

# Check if tag already exists
$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 1: Update version
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

# launcher-manifest.json is stamped with the real version at package time
# (package-release.ps1), keeping the csproj as the single version source of
# truth. No mirror needed here.

# Step 2: Release build - abort the release if the version bump doesn't compile,
# before any tag or commit is created.
Write-Host "Building (Release)..." -ForegroundColor Cyan
pixi run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: release build failed (exit $LASTEXITCODE). Aborting release." -ForegroundColor Red
    exit 1
}

# Step 3: Generate CHANGELOG
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    # First release - write a basic changelog entry
    $date = Get-Date -Format 'yyyy-MM-dd'
    $firstEntry = "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n"
    Set-Content $changelogPath $firstEntry
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    $changelogArgs = @{
        ChangelogPath = $changelogPath
        Version = $Version
        ArtifactPaths = @(
            "src/",
            "cameraunlock-core",
            "scripts/install.cmd",
            "scripts/uninstall.cmd"
        )
    }
    New-ChangelogFromCommits @changelogArgs
}

# Step 4: Commit
Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath $changelogPath
git commit -m "Release v$Version"

# Step 5: Create tag (annotated)
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"

# Step 6: Push (atomic so the version commit and tag arrive together; otherwise
# a network failure between the two pushes leaves main on the remote without
# the trigger tag, and the release CI never fires.)
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push --atomic origin main $tagName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: atomic push failed (exit $LASTEXITCODE). Neither main nor the tag was published." -ForegroundColor Red
    Write-Host "Resolve the underlying issue, then re-run 'git push --atomic origin main $tagName'." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Release $tagName initiated!" -ForegroundColor Green
Write-Host ""
Write-Host "The GitHub Actions release workflow will now:" -ForegroundColor Yellow
Write-Host "  - Build the release" -ForegroundColor White
Write-Host "  - Generate release notes from commits" -ForegroundColor White
Write-Host "  - Create GitHub release with artifacts" -ForegroundColor White
Write-Host ""
Write-Host "Watch progress at:" -ForegroundColor Yellow
Write-Host "  https://github.com/itsloopyo/the-painscreek-killings-headtracking/actions" -ForegroundColor Cyan
