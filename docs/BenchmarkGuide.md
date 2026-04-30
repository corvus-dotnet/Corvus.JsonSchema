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

After making code generator changes, regenerate all C/ directories:

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