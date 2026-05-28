#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs AsyncAPI integration tests with Podman/Docker compatibility.

.DESCRIPTION
    This script normalizes the DOCKER_HOST environment variable for Podman on Windows
    (npipe:////./pipe/name → npipe://./pipe/name) to ensure compatibility with
    Docker.DotNet's NPipe handler, then runs the integration test suite.

.PARAMETER Filter
    Optional test filter. Defaults to "TestCategory=integration&TestCategory!=failing"

.PARAMETER Configuration
    Build configuration. Defaults to "Release"

.PARAMETER Framework
    Target framework. Defaults to "net10.0"

.EXAMPLE
    .\run-integration-tests.ps1

.EXAMPLE
    .\run-integration-tests.ps1 -Filter "FullyQualifiedName~NatsTransportTests"
#>

[CmdletBinding()]
param(
    [string]$Filter = "TestCategory=integration&TestCategory!=failing",
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"

# Normalize DOCKER_HOST for Podman on Windows compatibility
$originalDockerHost = $env:DOCKER_HOST
if ($originalDockerHost -and $originalDockerHost.StartsWith("npipe:////")) {
    $env:DOCKER_HOST = "npipe://" + $originalDockerHost.Substring("npipe:////".Length)
    Write-Host "✓ Normalized DOCKER_HOST from '$originalDockerHost' to '$env:DOCKER_HOST'" -ForegroundColor Green
}

# Build the integration test project
Write-Host "`n▶ Building integration tests..." -ForegroundColor Cyan
dotnet build tests\Corvus.Text.Json.AsyncApi.Transport.IntegrationTests -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Run the tests
Write-Host "`n▶ Running integration tests..." -ForegroundColor Cyan
dotnet test --project tests\Corvus.Text.Json.AsyncApi.Transport.IntegrationTests `
    -f $Framework `
    --filter $Filter `
    -c $Configuration `
    --no-build

$testExitCode = $LASTEXITCODE

# Restore original DOCKER_HOST if it was changed
if ($originalDockerHost -and $originalDockerHost.StartsWith("npipe:////")) {
    $env:DOCKER_HOST = $originalDockerHost
}

exit $testExitCode