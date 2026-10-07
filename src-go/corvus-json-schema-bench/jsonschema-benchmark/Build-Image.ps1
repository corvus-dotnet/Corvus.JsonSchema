<#
.SYNOPSIS
Builds the jsonschema-benchmark/corvus-go image from this checkout's library source, for local comparisons.

.PARAMETER Engine
The container engine (docker or podman).
#>
[CmdletBinding()]
param([string] $Engine = "podman")

$ErrorActionPreference = "Stop"
$context = Join-Path ([System.IO.Path]::GetTempPath()) "corvus-go-image"
Remove-Item -Recurse -Force $context -ErrorAction SilentlyContinue
New-Item -ItemType Directory $context | Out-Null
$library = Join-Path $PSScriptRoot "../../corvus-json-schema"
$staged = New-Item -ItemType Directory (Join-Path $context "corvus-json-schema")
# The library's sources without its tests, which the image does not build.
Get-ChildItem -File $library -Filter "*.go" | Where-Object { $_.Name -notlike "*_test.go" } | Copy-Item -Destination $staged
Copy-Item (Join-Path $library "go.mod") $staged
Copy-Item -Recurse (Join-Path $library "metaschemas") $staged
# Its internal packages, which are the regular expression engine and the Unicode tables.
foreach ($package in @(Get-ChildItem -Directory (Join-Path $library "internal"))) {
    $internal = New-Item -ItemType Directory (Join-Path $staged "internal/$($package.Name)")
    Get-ChildItem -File $package.FullName -Filter "*.go" | Where-Object { $_.Name -notlike "*_test.go" } | Copy-Item -Destination $internal
}
$bench = New-Item -ItemType Directory (Join-Path $context "bench")
Copy-Item (Join-Path $PSScriptRoot "main.go"), (Join-Path $PSScriptRoot "go.mod"), (Join-Path $PSScriptRoot "memory-wrapper.sh") $bench
Copy-Item (Join-Path $PSScriptRoot "Dockerfile.local") (Join-Path $context "Dockerfile")
& $Engine build -t jsonschema-benchmark/corvus-go $context
if ($LASTEXITCODE -ne 0) { throw "image build failed" }
