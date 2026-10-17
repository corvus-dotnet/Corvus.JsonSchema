<#
.SYNOPSIS
Builds the jsonschema-benchmark/corvus-ml image from this checkout's library source, for local comparisons.

.PARAMETER Engine
The container engine (docker or podman).
#>
[CmdletBinding()]
param([string] $Engine = "podman")

$ErrorActionPreference = "Stop"
$context = Join-Path ([System.IO.Path]::GetTempPath()) "corvus-ml-image"
Remove-Item -Recurse -Force $context -ErrorAction SilentlyContinue
New-Item -ItemType Directory $context | Out-Null
$library = Join-Path $PSScriptRoot "../../corvus-json-schema"
$staged = New-Item -ItemType Directory (Join-Path $context "corvus-json-schema")
# The library's sources without its tests, which the image does not build: the project file and the three libraries.
Copy-Item (Join-Path $library "dune-project"), (Join-Path $library "corvus-json-schema.opam"), `
    (Join-Path $library "corvus-json-schema.opam.template") $staged
foreach ($directory in @("src", "ecmaregex", "ucd")) {
    Copy-Item -Recurse (Join-Path $library $directory) $staged
}
$bench = New-Item -ItemType Directory (Join-Path $context "bench")
foreach ($file in @("corvus_ocaml_benchmark.ml", "clock_stubs.c", "dune", "dune-project", "memory-wrapper.sh")) {
    Copy-Item (Join-Path $PSScriptRoot $file) $bench
}
Copy-Item (Join-Path $PSScriptRoot "Dockerfile.local") (Join-Path $context "Dockerfile")
& $Engine build -t jsonschema-benchmark/corvus-ml $context
if ($LASTEXITCODE -ne 0) { throw "image build failed" }
