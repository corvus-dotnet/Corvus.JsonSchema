# Benchmark Guide

This document explains how to run, interpret, and maintain the Corvus.JsonSchema benchmarks.

## Overview

The `benchmarks/` directory contains BenchmarkDotNet projects that measure validation performance across 37+ real-world JSON schemas (Ansible, AWS CDK, GeoJson, Krakend, OpenAPI, etc.). Each benchmark model project follows a **Baseline vs Current** convention to detect performance regressions.

## Directory structure

```
benchmarks/
├── Corvus.Text.Json.Benchmarks/               # Main benchmark runner
│   ├── Program.cs                              # BenchmarkDotNet configuration
│   └── ValidationBenchmarks/
│       ├── BenchmarkSimpleValidation.cs         # Person schema comparison
│       ├── BenchmarkUnevaluatedProperties.cs
│       └── SourceMeta/                          # Per-schema validation benchmarks
│           ├── BenchmarkAnsibleMetaValidation.cs
│           ├── BenchmarkGeoJsonValidation.cs
│           └── ...
├── Corvus.Text.Json.Toon.Benchmarks/          # TOON encode/decode and size benchmarks
├── Corvus.Text.Json.Yaml.Benchmarks/          # YAML encode/decode benchmarks
├── Corvus.Text.Json.<Name>BenchmarkModels/     # Per-schema model projects
│   ├── <name>-schema.json                       # Source JSON Schema
│   ├── <name>-instances.jsonl                   # Test data (JSON Lines)
│   ├── B/                                       # Frozen baseline (NEVER regenerate)
│   └── C/                                       # Current generation (regenerate after codegen changes)
├── scripts/
│   └── Write-BenchmarkSummary.ps1              # Regression report generator
└── Corvus.Text.Json.Benchmarks.slnx            # Benchmark solution
```

## B/ (Baseline) vs C/ (Current) convention

Each benchmark model project has two subdirectories of generated code:

| Directory | Namespace | Root type | Purpose |
|-----------|-----------|-----------|---------|
| **B/** | `Corvus.<Name>Benchmark.Baseline` | `Schema` | Frozen CLI-generated code — **never regenerate** |
| **C/** | `Corvus.<Name>Benchmark.Current` | `<Name>Schema` | Regenerated from the current code generator |

The B/ directory represents a fixed comparison point. By never changing it, you can isolate the performance impact of code generator changes in C/.

> **Warning:** Never regenerate B/ directories. They are the frozen baseline.

## Running benchmarks

### Prerequisites

- .NET 10.0 SDK (primary benchmark target)
- Windows (benchmark workflow runs on `windows-latest`)
- Ensure the machine is idle — benchmarks require consistent CPU scheduling

### Command line

```powershell
cd benchmarks\Corvus.Text.Json.Benchmarks

# Run all benchmarks
dotnet run -c Release -f net10.0 -- --filter=* --buildTimeout 1200

# Run a specific benchmark class
dotnet run -c Release -f net10.0 -- --filter=*SimpleValidation* --buildTimeout 1200

# Run all SourceMeta benchmarks
dotnet run -c Release -f net10.0 -- --filter=*SourceMeta* --buildTimeout 1200

# Run a single schema benchmark
dotnet run -c Release -f net10.0 -- --filter=*BenchmarkAnsibleMetaValidation* --buildTimeout 1200
```

### Important flags

| Flag | Value | Why |
|------|-------|-----|
| `-c Release` | Required | BenchmarkDotNet only runs in Release configuration |
| `-f net10.0` | Required | Skips the interactive TFM selector prompt |
| `--buildTimeout 1200` | Required | Default 120s is too short for this solution with source generators |
| `--filter=<pattern>` | Recommended | Wildcard pattern to select benchmarks |

> **Note:** The `--buildTimeout` must be at least 1200 seconds. The default (120s) and even 600s will time out due to the source generator compilation overhead.

### GitHub Actions workflow

The `benchmarks.yml` workflow runs benchmarks on demand:

```yaml
# Trigger via GitHub Actions UI with optional filter
workflow_dispatch:
  inputs:
    filter:
      description: 'BenchmarkDotNet filter pattern'
      default: '*'
```

The workflow:
1. Restores baseline results from the GitHub Actions cache
2. Builds the benchmark solution
3. Runs benchmarks with `--buildTimeout 1200`
4. Generates a markdown summary comparing against the baseline
5. Saves results as the new baseline for the branch
6. Uploads BenchmarkDotNet artifacts

## Interpreting results

BenchmarkDotNet produces results in `BenchmarkDotNet.Artifacts/results/`:
- `*-report.csv` — tabular results
- `*-report-full.json` — detailed JSON with statistics
- `*-report.md` — markdown table

The `Write-BenchmarkSummary.ps1` script compares runs against a baseline:

```powershell
benchmarks\scripts\Write-BenchmarkSummary.ps1 `
    -ResultsDir benchmarks\Corvus.Text.Json.Benchmarks\BenchmarkDotNet.Artifacts\results `
    -BaselineDir benchmarks\baseline-results `
    -RegressionThreshold 5.0
```

A regression is flagged (⚠️) when Mean increases by more than the threshold percentage (default 5%).

## Regenerating C/ models

After making code generator changes, regenerate **all** C/ directories with the batch script
(`benchmarks/scripts/Regenerate-CurrentBenchmarks.ps1`):

```powershell
pwsh benchmarks/scripts/Regenerate-CurrentBenchmarks.ps1
```

It builds the generator, then for every `*BenchmarkModels` project reads the root namespace
(`Corvus.<Name>Benchmark.Current`) from the existing `C/` output, uses the root type `<Name>Schema`
(overridable in the script's `$Overrides` table) and the project's single `*-schema.json`, cleans `C/`,
regenerates with `--engine V5`, and flags any project whose regeneration is **not** additive-only for review.
It never touches `B/`.

To regenerate a single project by hand (the script automates exactly this per project):

```powershell
# 1. Clean the C/ directory (old files cause compilation errors)
Remove-Item -Recurse -Force benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C\*

# 2. Regenerate with the CLI tool
dotnet run --project src\Corvus.Json.CodeGenerator -f net10.0 -c Release -- `
    benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\<name>-schema.json `
    --rootNamespace Corvus.<Name>Benchmark.Current `
    --outputRootTypeName <Name>Schema `
    --outputPath benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C `
    --engine V5
```

### Concrete example (AnsibleMeta)

```powershell
Remove-Item -Recurse -Force benchmarks\Corvus.Text.Json.AnsibleMetaBenchmarkModels\C\*

dotnet run --project src\Corvus.Json.CodeGenerator -f net10.0 -c Release -- `
    benchmarks\Corvus.Text.Json.AnsibleMetaBenchmarkModels\ansible-meta-schema.json `
    --rootNamespace Corvus.AnsibleMetaBenchmark.Current `
    --outputRootTypeName AnsibleMetaSchema `
    --outputPath benchmarks\Corvus.Text.Json.AnsibleMetaBenchmarkModels\C `
    --engine V5
```

### Regeneration checklist

1. **Always clean C/ first** — stale files from previous generations cause compilation errors
2. **Use `--engine V5`** — current generation engine
3. **Match the namespace convention** — `Corvus.<Name>Benchmark.Current`
4. **Match the root type convention** — `<Name>Schema`
5. **Never touch B/** — the baseline is frozen
6. **Build and verify** — `dotnet build Corvus.Text.Json.Benchmarks.slnx -c Release`

## Adding a new benchmark schema

1. Create a new project: `benchmarks/Corvus.Text.Json.<Name>BenchmarkModels/`
2. Add the source schema JSON file
3. Generate B/ (baseline) code with the *current* code generator — this becomes the frozen baseline
4. Generate C/ (current) code with the same generator
5. Add test data as `<name>-instances.jsonl` (one JSON document per line)
6. Create a benchmark class in `ValidationBenchmarks/SourceMeta/Benchmark<Name>Validation.cs`
7. Add the model project to `Corvus.Text.Json.Benchmarks.slnx`

## BenchmarkDotNet configuration

The `Program.cs` configures:
- **Runtime:** .NET 10.0 (primary), with commented-out lines for .NET 9.0, 8.0, and Framework 4.8.1
- **Strategy:** Throughput mode with all outliers removed
- **Exporters:** Markdown and JSON
- **Logger:** Console output

To benchmark against multiple runtimes, uncomment the additional `AddJob()` lines in `Program.cs`.

## The .NET version-over-version series

With each .NET release we measure what the new runtime gives us "for free" and publish the result in the [JSON Schema Performance blog series](https://endjin.com/blog/how-dotnet-10-boosted-json-schema-performance-by-18-percent). Both series validate the same array of 10,000 small person documents, and run one BenchmarkDotNet job per runtime from a single host, so the runtime is the only variable.

| Series | Project | Runtimes |
|--------|---------|----------|
| V4 engine | `src-v4/Corvus.Json.Benchmarking` (`ValidateLargeArrayCorvusV4`) | .NET 8.0, 9.0, 10.0, 11.0 |
| V5 engine | `benchmarks/Corvus.Text.Json.DotNetVersions.Benchmarks` | .NET 10.0, 11.0 |

The V5 project measures the generated types (`EvaluateSchema()`), the generated standalone evaluator, and the dynamic validator. Since 5.6.0 all three run the same compiled-plan evaluator, so expect them to agree. It is built once for `net10.0` and the same binaries run on each runtime.

The published figures come from BenchmarkDotNet, with each runtime job run in six separate processes. Both harnesses take `--launches N` for that. BenchmarkDotNet's own `--launchCount` adds another job rather than changing the runtime jobs that `Program.cs` defines.

```powershell
# V4 series
cd src-v4/Corvus.Json.Benchmarking
dotnet run -c Release -f net10.0 -- --filter '*ValidateLargeDocumentCorvusOnly.ValidateLargeArrayCorvusV4' --launches 6

# V5 series
cd benchmarks/Corvus.Text.Json.DotNetVersions.Benchmarks
dotnet run -c Release -f net10.0 -- --filter '*' --launches 6
```

The launch count matters. A process can settle into a slightly faster or slower state for its whole life, and the difference between two launches of the same runtime can be as large as the few percent between adjacent runtimes. Two single-launch runs of one benchmark gave ratios of 1.09 and 0.86 for the same pair of runtimes, each with a tiny error. Run the whole series twice and check that the older steps (.NET 8 to 9, and 9 to 10) reproduce before you trust the newest one.

`benchmarks/scripts/Run-DotNetVersionSeries.ps1` wraps a run. It builds the harnesses, waits for the machine to settle, pins the run to the performance cores, passes `--launches` (six by default), and probes the host's health before and after each series.

```powershell
pwsh benchmarks/scripts/Run-DotNetVersionSeries.ps1
```

Make sure the machine really is quiet, on the host as well as in the guest. A Windows-side `wsl.exe` console relay that was spinning at two and a half cores cost us a day of measurements, and nothing inside WSL could see it. Check the host's per-process CPU before a run.

Every runtime in the series must be installed alongside the SDK that `global.json` pins. BenchmarkDotNet 0.15.8 has no .NET 11 runtime moniker, so its .NET 11.0 job names its toolchain directly. The summary table's `Runtime` column reports the host's runtime for that job. The `Job` column and the legend above the table carry the runtime that actually ran.

On a hybrid CPU the run has to stay on the performance cores. Under WSL2 the guest's CPU numbers have no fixed relation to the host's cores, so pinning inside the guest is not enough. Set the affinity of the `vmmemWSL` process on the Windows host from an elevated PowerShell (`(Get-Process vmmemWSL).ProcessorAffinity = 0xFFF` for performance cores on logical processors 0 to 11), re-apply it after every `wsl --shutdown`, and keep the host's display awake. Compare the probe lines in `series.log` between runs. If the overhead figure has drifted up by 20% or more, the host has slowed down and the run should be repeated.

When a new .NET version ships, add a job for it to each `Program.cs`.