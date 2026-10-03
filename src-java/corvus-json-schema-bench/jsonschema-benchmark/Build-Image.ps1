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
Copy-Item (Join-Path $PSScriptRoot "Main.java"), (Join-Path $PSScriptRoot "Train.java"), (Join-Path $PSScriptRoot "Dockerfile") $context
@'
#!/bin/sh
OUTPUT=$(/usr/bin/time -f %M,%x -o /dev/stdout -q "$@" | sed '$!N; s/\n/,/; P; D')
EXIT_STATUS="${OUTPUT##*,}"
echo "$OUTPUT" | sed 's/,[^,]*$//'
exit $EXIT_STATUS
'@.Replace("`r`n", "`n") | Set-Content -NoNewline (Join-Path $context "memory-wrapper.sh")
& chmod +x (Join-Path $context "memory-wrapper.sh")
& $Engine build -t jsonschema-benchmark/corvus-java $context
if ($LASTEXITCODE -ne 0) { throw "image build failed" }
