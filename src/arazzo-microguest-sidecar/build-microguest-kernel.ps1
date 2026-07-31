#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the Arazzo micro-guest Unikraft kernel (ADR 0063).

.DESCRIPTION
    Builds the pinned kernel-builder image (kernel/kernel-builder.Dockerfile: kraft-hyperlight from the
    danbugs/kraftkit hyperlight-platform branch on Go >= 1.26), then runs the kernel build inside it from
    kernel/kraft.yaml — the kconfig the ADR 0063 spike proved for a .NET 10 Native-AOT static musl guest.
    The kernel lands at kernel/.unikraft/build/arazzo-microguest_hyperlight-x86_64 and is what the sidecar's
    --kernel argument points at. A deployment builds it once (or on a deliberate upstream pin move).

.PARAMETER BuilderTag
    The kernel-builder image tag. Defaults to arazzo-microguest-kernel-builder.

.PARAMETER ContainerCli
    The container CLI to use. Defaults to podman.

.EXAMPLE
    ./build-microguest-kernel.ps1
    Builds the builder image (cached after the first run) and then the kernel.
#>
[CmdletBinding()]
param(
    [string]$BuilderTag = 'arazzo-microguest-kernel-builder',
    [string]$ContainerCli = 'podman'
)

$ErrorActionPreference = 'Stop'
$kernelDir = Join-Path $PSScriptRoot 'kernel'

Write-Host "Building $BuilderTag from kernel/kernel-builder.Dockerfile using $ContainerCli..."
& $ContainerCli build -f (Join-Path $kernelDir 'kernel-builder.Dockerfile') -t $BuilderTag $kernelDir
if ($LASTEXITCODE -ne 0) {
    throw "Kernel-builder image build failed (exit code $LASTEXITCODE)."
}

Write-Host 'Building the micro-guest kernel (kraft-hyperlight build)...'
& $ContainerCli run --rm -v "${kernelDir}:/work" -w /work $BuilderTag kraft-hyperlight build --plat hyperlight --arch x86_64
if ($LASTEXITCODE -ne 0) {
    throw "Kernel build failed (exit code $LASTEXITCODE)."
}

$kernel = Join-Path $kernelDir '.unikraft/build/arazzo-microguest_hyperlight-x86_64'
if (-not (Test-Path $kernel)) {
    throw "The kernel build reported success but $kernel does not exist."
}

Write-Host "Built $kernel."
