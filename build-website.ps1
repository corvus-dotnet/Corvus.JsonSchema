<#
.SYNOPSIS
    CI wrapper script for building the documentation website in a parallel job.
.DESCRIPTION
    This script is called by the post-compile CI job to build the documentation
    website using pre-built .NET assemblies from the compile cache. It delegates
    to docs/website/build.ps1 with -SkipDotNetBuild and passes through the
    BasePathPrefix and IsPreviewDeployment settings from environment variables.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $PSCommandPath
$websiteDir = Join-Path $here "docs\website"

$websiteBuildArgs = @("-SkipDotNetBuild")

$basePathPrefix = $env:BUILDVAR_BasePathPrefix
if ($basePathPrefix) {
    $websiteBuildArgs += "-BasePathPrefix", $basePathPrefix
}

if ($env:BUILDVAR_IsPreviewDeployment -eq "true" -or $env:BUILDVAR_IsPreviewDeployment -eq "True") {
    $websiteBuildArgs += "-IsPreviewDeployment"
}

Write-Host "Building documentation website..."
Write-Host "  BasePathPrefix: $basePathPrefix"
Write-Host "  IsPreviewDeployment: $($env:BUILDVAR_IsPreviewDeployment)"
Write-Host "  Args: $websiteBuildArgs"

& pwsh -File (Join-Path $websiteDir "build.ps1") @websiteBuildArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
