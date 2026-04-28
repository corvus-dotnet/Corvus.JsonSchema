---
name: corvus-benchmarks
description: >
  Run, interpret, and maintain BenchmarkDotNet benchmarks for JSON Schema validation
  and query languages. Covers the B/ (frozen baseline) vs C/ (current) directory
  convention, stale Job-* cleanup, --buildTimeout, result file polling, regenerating
  C/ models after codegen changes, and JSONata/JMESPath/JsonLogic/JSONPath benchmarks.
  USE FOR: running benchmarks, interpreting results, regenerating benchmark models,
  troubleshooting BDN issues, adding new benchmark schemas.
  DO NOT USE FOR: general .NET performance analysis
  (use the analyzing-dotnet-performance skill).
---

# BenchmarkDotNet Benchmarks

## B/ vs C/ Convention

Each benchmark model project has two subdirectories:

| Directory | Purpose | Namespace | Root type |
|-----------|---------|-----------|-----------|
| **B/** | Frozen baseline — **NEVER regenerate** | `Corvus.<Name>Benchmark.Baseline` | `Schema` |
| **C/** | Current — regenerate after codegen changes | `Corvus.<Name>Benchmark.Current` | `<Name>Schema` |

37+ benchmark model projects follow this pattern with no exceptions.

## Running Benchmarks

### Step-by-Step Procedure

```powershell
# 1. Build in Release
dotnet build Corvus.Text.Json.slnx -c Release -v q

# 2. Run correctness tests first
dotnet test Corvus.Text.Json.slnx -c Release -f net10.0 --filter "category!=failing&category!=outerloop" -v q

# 3. CRITICAL — Clean stale Job-* directories
$benchDir = "benchmarks\Corvus.Text.Json.Benchmarks"
Remove-Item "$benchDir\bin\Release\net10.0\Job-*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$benchDir\BenchmarkDotNet.Artifacts\results\*" -Force -ErrorAction SilentlyContinue

# 4. Run benchmarks
cd $benchDir
dotnet run -c Release -f net10.0 -- --filter '*' --buildTimeout 1200
```

### Critical Rules

1. **Always clean `Job-*` directories** before running. Stale ones cause file locks; BDN silently drops benchmarks.
2. **Never pipe BDN output through truncating commands** (`Select-Object -First N`). This kills the host process.
3. **`--filter '*'` must be single-quoted** in PowerShell to prevent glob expansion.
4. **`--buildTimeout 1200`** is required — source generators make default 120s too short.
5. **Detect completion by polling for result files**, not by waiting on shell output:
   ```powershell
   Get-ChildItem "$benchDir\BenchmarkDotNet.Artifacts\results\*-report-default.md"
   ```

## Result Locations

Results are at `benchmarks/<Project>/BenchmarkDotNet.Artifacts/results/` (**not** the repo root):
- `*-report-default.md` — markdown reports (one per benchmark class)
- `*-report-full.json` — JSON reports

## Regenerating C/ Models

After code generator changes, regenerate all C/ directories:

```powershell
# Clean C/ first
Remove-Item -Recurse -Force benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C\*

# Regenerate
dotnet run --project src\Corvus.Json.CodeGenerator -f net10.0 -c Release -- `
    <schema-path> `
    --rootNamespace Corvus.<Name>Benchmark.Current `
    --outputRootTypeName <Name>Schema `
    --outputPath benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C `
    --engine V5
```

## JSONata/JMESPath/JsonLogic/JSONPath Benchmarks

| Project | Benchmarks | Build timeout |
|---------|-----------|---------------|
| `Corvus.Text.Json.Jsonata.Benchmarks` | 62 (20 CG + 20 RT + 22 Native) | Pre-configured 15 min |
| `Corvus.Text.Json.JMESPath.Benchmarks` | JMESPath comparison | Pre-configured |
| `Corvus.Text.Json.JsonLogic.Benchmarks` | JsonLogic comparison | Pre-configured |
| `Corvus.Text.Json.JsonPath.Benchmarks` | JSONPath RT + CG vs JsonEverything | Pre-configured |

## Other Benchmark Projects

| Project | What it benchmarks |
|---------|-------------------|
| `Corvus.Text.Json.Yaml.Benchmarks` | YAML conversion performance |
| `Corvus.Numerics.Benchmarks` | BigNumber arithmetic performance |
| `Corvus.Json.Validator.Benchmarks` | Dynamic validator performance |
| `Corvus.Text.Json.CodeGeneration.Benchmarks` | Code generation pipeline performance |
| `Corvus.Text.Json.Benchmarks.Validation` | Standalone evaluator validation benchmarks |

**JSONata method naming:** `Corvus_<Cat>` (RT), `Corvus_CodeGen_<Cat>` (CG), `Native_<Cat>` (baseline). This naming convention is specific to the JSONata benchmarks.

Generate comparison table: `node benchmarks/bench_table.js`

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Fewer benchmarks than expected | Stale `Job-*` dirs | Clean and re-run |
| BDN build exits code 1 | File lock from prior run | Clean `Job-*` dirs |
| No source-generated methods | Source generator didn't run | Build in Release, check `obj/` |
| Results in wrong directory | Looking at repo root | Check `benchmarks/.../BenchmarkDotNet.Artifacts/results/` |

## Cross-References
- For build commands, see `corvus-build-and-test`
- For codegen (to regenerate C/), see `corvus-codegen`
- Full guide: `docs/BenchmarkGuide.md`
