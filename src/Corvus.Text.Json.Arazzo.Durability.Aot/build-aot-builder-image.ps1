#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the Arazzo AOT builder container image from aot-builder.Dockerfile.

.DESCRIPTION
    The workflow AOT builder (ADR 0055) runs this image to native-AOT compile a workflow version's serverless
    host-app. A deployment builds the image once (or on runtime upgrade) and the control plane's ContainerWorkflowAotBuilder
    runs it per (environment, version, runtime target). Cross-platform PowerShell; drives podman or docker.

.PARAMETER Tag
    The image tag to build. Defaults to arazzo-aot-builder:net10 (the tag ContainerWorkflowAotBuilder defaults to).

.PARAMETER ContainerCli
    The container CLI to use. Defaults to podman.

.EXAMPLE
    ./build-aot-builder-image.ps1
    Builds arazzo-aot-builder:net10 with podman.
#>
[CmdletBinding()]
param(
    [string]$Tag = 'arazzo-aot-builder:net10',
    [string]$ContainerCli = 'podman'
)

$ErrorActionPreference = 'Stop'
$dockerfile = Join-Path $PSScriptRoot 'aot-builder.Dockerfile'

Write-Host "Building $Tag from $dockerfile using $ContainerCli..."
& $ContainerCli build -f $dockerfile -t $Tag $PSScriptRoot
if ($LASTEXITCODE -ne 0) {
    throw "Container image build failed (exit code $LASTEXITCODE)."
}

Write-Host "Built $Tag."
