<#
.SYNOPSIS
    Packs all Corvus.Text.Json V5 (and required V4) NuGet packages to a local feed.

.DESCRIPTION
    Builds and packs the Corvus.Text.Json.slnx solution into a local packages folder.
    This lets you test pre-release changes in consuming projects without publishing
    to any remote feed.

    Run this script from the repository root, or from anywhere — it locates the
    solution relative to its own location ($PSScriptRoot).

.PARAMETER Version
    The package version to use. Defaults to 5.0.0-local.1.
    Increment the suffix number on each repack to avoid NuGet cache issues
    (e.g. 5.0.0-local.2, 5.0.0-local.3).

.PARAMETER OutputDir
    The directory to write .nupkg files to. Defaults to local-packages/ in the
    repository root. The directory is created automatically if it does not exist.

.EXAMPLE
    .\build-local-packages.ps1
    Packs all projects to .\local-packages with version 5.0.0-local.1.

.EXAMPLE
    .\build-local-packages.ps1 -Version 5.0.0-local.3
    Packs all projects with an incremented version to avoid cache hits.

.EXAMPLE
    .\build-local-packages.ps1 -OutputDir C:\feeds\local
    Packs to a custom output directory.
#>
param(
    [string]$Version = "5.0.0-local.1",
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "local-packages"
}

$solution = Join-Path $repoRoot "Corvus.Text.Json.slnx"

if (-not (Test-Path $solution)) {
    Write-Error "Solution not found at $solution"
    exit 1
}

# Ensure output directory exists
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "Packing $solution..." -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Gray
Write-Host "  Output:  $OutputDir" -ForegroundColor Gray
Write-Host ""

dotnet pack "$solution" -c Release --output "$OutputDir" /p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nPack failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
$count = (Get-ChildItem $OutputDir -Filter "*.nupkg").Count
Write-Host "Packed $count packages to $OutputDir" -ForegroundColor Green
