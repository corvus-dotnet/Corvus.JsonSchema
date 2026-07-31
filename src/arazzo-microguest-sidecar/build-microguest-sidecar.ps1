#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the Arazzo micro-guest sidecar binary (ADR 0063).

.DESCRIPTION
    Compiles the sidecar as a static musl binary inside the pinned rust:1.89-alpine container (the same
    hermetic-container discipline as the AOT builder images), leaving it at target/release/
    arazzo-microguest-sidecar. The committed Cargo.lock pins the whole dependency graph, including
    hyperlight-unikraft (exactly 0.12.1); upgrades are deliberate, tested moves (ADR 0063).

.PARAMETER ContainerCli
    The container CLI to use. Defaults to podman.

.PARAMETER CargoCacheDir
    A host directory reused as the cargo registry cache across builds. Defaults to
    .cargo-registry-cache under this directory (created on first use, git-ignored).

.EXAMPLE
    ./build-microguest-sidecar.ps1
    Builds target/release/arazzo-microguest-sidecar with podman.
#>
[CmdletBinding()]
param(
    [string]$ContainerCli = 'podman',
    [string]$CargoCacheDir
)

$ErrorActionPreference = 'Stop'
if (-not $CargoCacheDir) {
    $CargoCacheDir = Join-Path $PSScriptRoot '.cargo-registry-cache'
}

New-Item -ItemType Directory -Force -Path $CargoCacheDir | Out-Null

Write-Host "Building the sidecar (release, static musl) in rust:1.89-alpine using $ContainerCli..."
& $ContainerCli run --rm `
    -v "${PSScriptRoot}:/work" `
    -v "${CargoCacheDir}:/usr/local/cargo/registry" `
    -w /work `
    rust:1.89-alpine `
    sh -c 'apk add --no-cache musl-dev clang lld >/dev/null && cargo build --release --locked'
if ($LASTEXITCODE -ne 0) {
    throw "Sidecar build failed (exit code $LASTEXITCODE)."
}

$binary = Join-Path $PSScriptRoot 'target/release/arazzo-microguest-sidecar'
if (-not (Test-Path $binary)) {
    throw "The build reported success but $binary does not exist."
}

Write-Host "Built $binary."
