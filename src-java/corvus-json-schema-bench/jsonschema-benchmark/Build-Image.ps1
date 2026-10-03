<#
.SYNOPSIS
Builds the jsonschema-benchmark/corvus-java image from this checkout's library source, for local comparisons.

.PARAMETER Engine
The container engine (docker or podman).
#>
[CmdletBinding()]
param([string] $Engine = "podman")

$ErrorActionPreference = "Stop"
$context = Join-Path ([System.IO.Path]::GetTempPath()) "corvus-java-image"
Remove-Item -Recurse -Force $context -ErrorAction SilentlyContinue
New-Item -ItemType Directory $context | Out-Null
$library = Join-Path $PSScriptRoot "../../corvus-json-schema"
New-Item -ItemType Directory (Join-Path $context "corvus-json-schema") | Out-Null
Copy-Item -Recurse (Join-Path $library "src"), (Join-Path $library "pom.xml") (Join-Path $context "corvus-json-schema")
$bench = New-Item -ItemType Directory (Join-Path $context "bench")
Copy-Item -Recurse (Join-Path $PSScriptRoot "src"), (Join-Path $PSScriptRoot "pom.xml"), (Join-Path $PSScriptRoot "memory-wrapper.sh") $bench
Copy-Item (Join-Path $PSScriptRoot "Dockerfile.local") (Join-Path $context "Dockerfile")
& $Engine build -t jsonschema-benchmark/corvus-java $context
if ($LASTEXITCODE -ne 0) { throw "image build failed" }
