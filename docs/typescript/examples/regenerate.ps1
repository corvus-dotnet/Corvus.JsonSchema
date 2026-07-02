<#
.SYNOPSIS
    Regenerates every model recipe's generated.ts from its schema via the production CLI.

.DESCRIPTION
    For each `NNN-*` recipe directory, runs `corvusjson jsonschema --engine TypeScript`, keeps a single
    shared `corvus-runtime.ts` at the examples root, and repoints each module's runtime import at it.

    Cross-platform PowerShell (run with `pwsh`). Verify the result with:
        npm run build; node dist/<recipe>/demo.js
#>
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path # repo (worktree) root

Write-Host 'building CLI (Debug)...'
dotnet build (Join-Path $root 'src/Corvus.Json.Cli/Corvus.Json.Cli.csproj') -c Debug | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'CLI build failed' }
$dll = Join-Path $root 'src/Corvus.Json.Cli/bin/Debug/net10.0/Corvus.Json.Cli.dll'

$runtimeSaved = $false
foreach ($dir in Get-ChildItem -Path $here -Directory | Where-Object { $_.Name -match '^[0-9].*-' } | Sort-Object Name) {
    $schema = Get-ChildItem -Path $dir.FullName -Filter '*.json' | Select-Object -First 1
    Write-Host "  $($dir.Name)  <-  $($schema.Name)"
    dotnet $dll jsonschema $schema.FullName --engine TypeScript --outputPath $dir.FullName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "generation failed for $($dir.Name)" }

    $runtimeTs = Join-Path $dir.FullName 'corvus-runtime.ts'
    if (-not $runtimeSaved) {
        Move-Item -Force -Path $runtimeTs -Destination (Join-Path $here 'corvus-runtime.ts') # keep one shared copy
        $runtimeSaved = $true
    }
    else {
        Remove-Item -Force -Path $runtimeTs -ErrorAction SilentlyContinue
    }

    $generated = Join-Path $dir.FullName 'generated.ts'
    (Get-Content -Raw -Path $generated) -replace '"\./corvus-runtime\.js"', '"../corvus-runtime.js"' |
        Set-Content -NoNewline -Encoding utf8 -Path $generated
}

Write-Host 'regenerated. verify with: npm run build; node dist/<recipe>/demo.js'
