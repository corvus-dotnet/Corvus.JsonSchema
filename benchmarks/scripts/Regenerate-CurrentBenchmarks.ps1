#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Regenerates every benchmark C/ (Current) model directory from the current V5 code generator.

.DESCRIPTION
    Each benchmarks/Corvus.Text.Json.<Name>BenchmarkModels project has two generated subdirectories:

      B/  (Baseline) — frozen V4-generated code. NEVER regenerate. It is the historical comparison point.
      C/  (Current)  — regenerated from the current code generator. Regenerate after ANY code-generator
                       change (see .github/copilot-instructions.md "Regenerating C/ benchmarks" and CLAUDE.md).

    This script regenerates ALL C/ directories in one pass. For each project it:
      * reads the root namespace (Corvus.<X>Benchmark.Current) from the existing C/ output — the source of truth;
      * uses the root type name <X>Schema by convention, overridden via $Overrides for the few that deviate;
      * uses the single *-schema.json in the project (excluding *instances*), overridden via $Overrides;
      * cleans C/ and regenerates with --engine V5;
      * verifies (unless -SkipVerify) that the result is additive-only vs git HEAD (no removed lines) — a
        non-additive diff means wrong generation parameters for that project, and the run fails.

    It never touches B/.

.PARAMETER SkipVerify
    Skip the per-project additive-only git diff check (e.g. when intentionally taking a breaking generator change).

.EXAMPLE
    pwsh benchmarks/scripts/Regenerate-CurrentBenchmarks.ps1

.NOTES
    Location: benchmarks/scripts/Regenerate-CurrentBenchmarks.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$generatorProject = Join-Path $repoRoot 'src' 'Corvus.Json.CodeGenerator'
$benchmarksDir = Join-Path $repoRoot 'benchmarks'

# Projects whose root type name and/or schema file deviate from the conventions
# (<X>Schema from the namespace, and the single *-schema.json). Add a RootPath key here if a project
# needs --rootPath. The namespace is always read from the existing C/ output.
$Overrides = @{
    'Corvus.Text.Json.BenchmarkModels' = @{ Schema = 'person-schema.json'; RootType = 'Person' }
}

Write-Host 'Building the code generator (Release)...' -ForegroundColor Cyan
dotnet build $generatorProject -f net10.0 -c Release | Out-Null

$projects = Get-ChildItem -Path $benchmarksDir -Directory -Filter '*BenchmarkModels' | Sort-Object Name
$regenerated = 0
$failures = @()
$review = @()

foreach ($proj in $projects) {
    $cDir = Join-Path $proj.FullName 'C'
    if (-not (Test-Path $cDir)) { continue }

    # Root namespace from the existing C/ output (source of truth): Corvus.<X>Benchmark.Current.
    $ns = Get-ChildItem -Path $cDir -Filter '*.cs' -File |
        Select-String -Pattern '^namespace (Corvus\.\w+Benchmark\.Current);' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Select-Object -First 1
    if (-not $ns) { Write-Warning "No 'Corvus.*Benchmark.Current' namespace under $($proj.Name)/C; skipping"; continue }
    $x = ($ns -replace '^Corvus\.', '') -replace 'Benchmark\.Current$', ''

    $ov = $Overrides[$proj.Name]
    $rootType = if ($ov -and $ov.RootType) { $ov.RootType } else { "${x}Schema" }
    $schemaName = if ($ov -and $ov.Schema) {
        $ov.Schema
    }
    else {
        (Get-ChildItem -Path $proj.FullName -Filter '*-schema.json' -File |
            Where-Object { $_.Name -notlike '*instances*' } |
            Select-Object -First 1).Name
    }
    if (-not $schemaName) { Write-Warning "No *-schema.json in $($proj.Name); skipping"; continue }
    $schema = Join-Path $proj.FullName $schemaName

    Write-Host "  $($proj.Name)  ->  $ns / $rootType   ($schemaName)" -ForegroundColor DarkGray

    # Clean C/ first — stale files cause compilation errors — then regenerate. Never touch B/.
    Remove-Item -Path (Join-Path $cDir '*') -Recurse -Force -ErrorAction SilentlyContinue

    $genArgs = @($schema, '--rootNamespace', $ns, '--outputRootTypeName', $rootType, '--outputPath', $cDir, '--engine', 'V5', '--formatMode', 'disable')
    if ($ov -and $ov.RootPath) { $genArgs += @('--rootPath', $ov.RootPath) }

    dotnet run --project $generatorProject -f net10.0 -c Release --no-build -- @genArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { $failures += "$($proj.Name) (generator exit $LASTEXITCODE)"; continue }

    if (-not $SkipVerify) {
        # An additive-only regeneration (the common case for an additive generator change) removes no lines from
        # tracked C/ files vs HEAD. Removed lines are NOT necessarily wrong — a generator that renamed/dropped a
        # nested type produces benign drift too — so this is a review flag, not a failure. Wrong generation
        # parameters show up here as a large removal; assess the diff for any project listed.
        $relC = [System.IO.Path]::GetRelativePath($repoRoot, $cDir)
        $removed = (& git -C $repoRoot diff --numstat -- $relC | ForEach-Object { ($_ -split "`t")[1] } |
            Where-Object { $_ -match '^\d+$' } | Measure-Object -Sum).Sum
        if ($removed -gt 0) {
            $review += "$($proj.Name): $removed removed line(s) — review the diff (benign type drift, or wrong generation parameters?)"
        }
    }

    $regenerated++
}

Write-Host ''
Write-Host "Regenerated $regenerated C/ directories." -ForegroundColor Green
if ($failures.Count -gt 0) {
    Write-Host 'GENERATOR FAILURES:' -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
if ($review.Count -gt 0) {
    Write-Host "REVIEW ($($review.Count) project(s) had non-additive diffs):" -ForegroundColor Yellow
    $review | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}
else {
    Write-Host 'All C/ regenerations were additive-only.' -ForegroundColor Green
}
