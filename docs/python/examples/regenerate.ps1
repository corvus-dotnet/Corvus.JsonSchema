<#
.SYNOPSIS
    Regenerates every model recipe's generated/ package from its schema via the production CLI, and (with
    -Check) type-checks each recipe with mypy --strict and pyright, then runs its demo.

.DESCRIPTION
    For each `NNN-*` recipe directory, runs `corvusjson jsonschema --engine Python` into `<recipe>/generated`.
    The pure-Python runtime (`corvus_json_runtime`) is resolved off PYTHONPATH from packages/, so nothing needs
    installing. Override the interpreter with the CORVUS_PYTHON environment variable (default: `python`);
    pyright is run only when it is on PATH.

    Cross-platform PowerShell (run with `pwsh`).
#>
[CmdletBinding()]
param([switch] $Check)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path # repo (worktree) root
$python = if ($env:CORVUS_PYTHON) { $env:CORVUS_PYTHON } else { 'python' }
$runtimeSrc = Join-Path $root 'packages/corvus-json-runtime-py/src'

Write-Host 'building CLI (Debug)...'
dotnet build (Join-Path $root 'src/Corvus.Json.Cli/Corvus.Json.Cli.csproj') -c Debug | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'CLI build failed' }
$dll = Join-Path $root 'src/Corvus.Json.Cli/bin/Debug/net10.0/Corvus.Json.Cli.dll'

foreach ($dir in Get-ChildItem -Path $here -Directory | Where-Object { $_.Name -match '^[0-9].*-' } | Sort-Object Name) {
    $schema = Get-ChildItem -Path $dir.FullName -Filter '*.json' | Select-Object -First 1
    $gen = Join-Path $dir.FullName 'generated'
    Write-Host "  $($dir.Name)  <-  $($schema.Name)"
    dotnet $dll jsonschema $schema.FullName --engine Python --outputPath $gen | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "generation failed for $($dir.Name)" }

    if ($Check) {
        $env:PYTHONPATH = "$runtimeSrc$([System.IO.Path]::PathSeparator)$($dir.FullName)"
        & $python -m mypy --strict (Join-Path $dir.FullName 'demo.py') (Join-Path $gen '__init__.py')
        if ($LASTEXITCODE -ne 0) { throw "mypy failed for $($dir.Name)" }
        if (Get-Command pyright -ErrorAction SilentlyContinue) {
            & pyright --pythonpath $python (Join-Path $dir.FullName 'demo.py') (Join-Path $gen '__init__.py')
            if ($LASTEXITCODE -ne 0) { throw "pyright failed for $($dir.Name)" }
        }
        Push-Location $dir.FullName
        try { & $python demo.py | Out-Null; if ($LASTEXITCODE -ne 0) { throw "demo failed for $($dir.Name)" } }
        finally { Pop-Location }
    }
}

Write-Host 'regenerated. verify with: pwsh ./regenerate.ps1 -Check'
