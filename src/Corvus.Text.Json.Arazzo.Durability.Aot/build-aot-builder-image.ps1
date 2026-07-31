#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds an Arazzo AOT builder container image.

.DESCRIPTION
    The workflow AOT builder (ADR 0055) runs these images to native-AOT compile a workflow version's serverless
    host-app. A deployment builds the image once (or on runtime upgrade) and the control plane's ContainerWorkflowAotBuilder
    runs it per (environment, version, runtime target). The al2023 variant (aot-builder.Dockerfile) builds the cloud
    Linux targets; the alpine variant (aot-builder-alpine.Dockerfile) builds the micro-guest's fully static musl
    target (ADR 0063). Cross-platform PowerShell; drives podman or docker.

.PARAMETER Variant
    Which builder image to build: al2023 (the default, for the glibc cloud targets) or alpine (the micro-guest's
    static musl target).

.PARAMETER Tag
    The image tag to build. Defaults per variant to the tag ContainerWorkflowAotBuilder configurations use:
    arazzo-aot-builder:net10 (al2023) or arazzo-aot-builder:net10-alpine (alpine).

.PARAMETER ContainerCli
    The container CLI to use. Defaults to podman.

.EXAMPLE
    ./build-aot-builder-image.ps1
    Builds arazzo-aot-builder:net10 with podman.

.EXAMPLE
    ./build-aot-builder-image.ps1 -Variant alpine
    Builds arazzo-aot-builder:net10-alpine with podman.
#>
[CmdletBinding()]
param(
    [ValidateSet('al2023', 'alpine')]
    [string]$Variant = 'al2023',
    [string]$Tag,
    [string]$ContainerCli = 'podman'
)

$ErrorActionPreference = 'Stop'
if ($Variant -eq 'alpine') {
    $dockerfileName = 'aot-builder-alpine.Dockerfile'
    if (-not $Tag) { $Tag = 'arazzo-aot-builder:net10-alpine' }
}
else {
    $dockerfileName = 'aot-builder.Dockerfile'
    if (-not $Tag) { $Tag = 'arazzo-aot-builder:net10' }
}

$dockerfile = Join-Path $PSScriptRoot $dockerfileName

Write-Host "Building $Tag from $dockerfile using $ContainerCli..."
& $ContainerCli build -f $dockerfile -t $Tag $PSScriptRoot
if ($LASTEXITCODE -ne 0) {
    throw "Container image build failed (exit code $LASTEXITCODE)."
}

Write-Host "Built $Tag."
