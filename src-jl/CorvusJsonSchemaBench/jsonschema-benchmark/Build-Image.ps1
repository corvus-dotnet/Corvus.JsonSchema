<#
.SYNOPSIS
Builds the jsonschema-benchmark/corvus-jl image from this checkout's package source, for local comparisons.

.PARAMETER Engine
The container engine (docker or podman).

.PARAMETER SystemImage
Builds jsonschema-benchmark/corvus-jl-sysimage instead: the package and the program compiled into a Julia system
image with PackageCompiler (Dockerfile.sysimage). The README says what that changes.
#>
[CmdletBinding()]
param(
    [string] $Engine = "podman",
    [switch] $SystemImage
)

$ErrorActionPreference = "Stop"
$context = Join-Path ([System.IO.Path]::GetTempPath()) "corvus-jl-image"
Remove-Item -Recurse -Force $context -ErrorAction SilentlyContinue
New-Item -ItemType Directory $context | Out-Null
$library = Join-Path $PSScriptRoot "../../CorvusJsonSchema"
$staged = New-Item -ItemType Directory (Join-Path $context "CorvusJsonSchema")
# The package's sources (with the metaschemas it embeds) without its tests, which the image does not run.
Copy-Item (Join-Path $library "Project.toml") $staged
Copy-Item -Recurse (Join-Path $library "src") $staged
$bench = New-Item -ItemType Directory (Join-Path $context "bench")
Copy-Item (Join-Path $PSScriptRoot "Project.toml"), (Join-Path $PSScriptRoot "main.jl"), (Join-Path $PSScriptRoot "memory-wrapper.sh") $bench
Copy-Item -Recurse (Join-Path $PSScriptRoot "src") $bench
if ($SystemImage) {
    Copy-Item (Join-Path $PSScriptRoot "sysimage.jl") $bench
    Copy-Item (Join-Path $PSScriptRoot "Dockerfile.sysimage") (Join-Path $context "Dockerfile")
    $tag = "jsonschema-benchmark/corvus-jl-sysimage"
}
else {
    Copy-Item (Join-Path $PSScriptRoot "Dockerfile.local") (Join-Path $context "Dockerfile")
    $tag = "jsonschema-benchmark/corvus-jl"
}
& $Engine build -t $tag $context
if ($LASTEXITCODE -ne 0) { throw "image build failed" }
