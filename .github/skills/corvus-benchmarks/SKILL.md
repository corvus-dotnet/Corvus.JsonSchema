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
dotnet test --solution Corvus.Text.Json.slnx -c Release -f net10.0 --filter "TestCategory!=failing&TestCategory!=outerloop"

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
| `Corvus.Text.Json.Jsonata.Benchmarks` | 62 (20 CG + 20 RT + 22 Native) | 15 min in `Program.cs` |
| `Corvus.Text.Json.JMESPath.Benchmarks` | JMESPath comparison | In `Program.cs` |
| `Corvus.Text.Json.JsonLogic.Benchmarks` | JsonLogic comparison | In `Program.cs` |
| `Corvus.Text.Json.JsonPath.Benchmarks` | JSONPath RT + CG vs JsonEverything | In `Program.cs` |

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

## Benchmarks and BenchmarkDotNet (moved from copilot-instructions.md)

The `benchmarks/` directory contains BenchmarkDotNet projects that compare validation performance against a frozen baseline. Each benchmark model project (e.g., `Corvus.Text.Json.AnsibleMetaBenchmarkModels`) has two subdirectories:

- **B/ (Baseline)** — frozen, CLI-generated code. **Never regenerate B/.** It represents the fixed comparison point.
- **C/ (Current)** — regenerated from the current code generator after changes. Always regenerate C/ when codegen changes.

### Namespace and root type conventions

| Directory | Namespace | Root type |
|---|---|---|
| B/ | `Corvus.<Name>Benchmark.Baseline` | `Schema` |
| C/ | `Corvus.<Name>Benchmark.Current` | `<Name>Schema` |

Where `<Name>` is the benchmark name (e.g., `AnsibleMeta`, `GeoJson`, `CmakePresets`).

### Regenerating C/ benchmarks

After making code generator changes, regenerate **all** C/ directories with the batch script:

```bash
pwsh benchmarks/scripts/Regenerate-CurrentBenchmarks.ps1
```

It builds the generator, then for every `*BenchmarkModels` project reads the root namespace
(`Corvus.<Name>Benchmark.Current`) from the existing `C/` output, applies the `<Name>Schema` root-type
convention (overridable via the script's `$Overrides` table) against the project's single `*-schema.json`,
cleans `C/`, regenerates with `--engine V5`, and flags any project whose regeneration is **not** additive-only
for review. It never touches B/. See `docs/BenchmarkGuide.md` for the full description.

> A non-additive (review-flagged) diff is not automatically wrong: a generator change that alters nested
> type-name truncation (e.g. the path-truncation collision fix in `GenerationDriverV5.cs`) legitimately
> renames deeply-nested files for the larger schemas (GeoJson, Ui5, CmakePresets, …), which git pairs as
> delete+add. Confirm the benchmark solution still builds and treat such a sweep as its own commit, distinct
> from any feature change riding alongside it.

To regenerate a single project by hand (the script automates exactly this per project):

```bash
# Clean the C/ directory first (old files cause compilation errors)
Remove-Item -Recurse -Force benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C\*

# Regenerate with CLI tool
dotnet run --project src\Corvus.Json.CodeGenerator -f net10.0 -c Release -- <schema-path> --rootNamespace Corvus.<Name>Benchmark.Current --outputRootTypeName <Name>Schema --outputPath benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C --engine V5
```

All 37+ benchmark models follow the same pattern — no special cases.

### Running benchmarks

```bash
cd benchmarks\Corvus.Text.Json.Benchmarks
dotnet run -c Release -f net10.0 -- --filter='*<SchemaName>*' --buildTimeout 1200
```

The `--buildTimeout 1200` flag is required because the default 120s is too short for this solution with source generators. Always ask the user to confirm their PC is idle before running benchmarks (they are CPU-intensive and results are unreliable under load).

## Running BenchmarkDotNet (BDN) projects

Multiple benchmark projects live under `benchmarks/`. They all use BDN with out-of-process toolchains. The same rules apply to every one of them.

### General procedure

```powershell
# 1. Build the projects under test in Release (must succeed before benchmarks)
dotnet build <relevant-src-projects> -c Release -v q --no-restore

# 2. Run the relevant tests to verify correctness before benchmarking
dotnet test --project <relevant-test-project> -f net10.0 --filter "TestCategory!=failing&TestCategory!=outerloop" --no-restore

# 3. Clean stale BDN artifacts (CRITICAL — stale Job-* dirs cause file locks)
$benchDir = "benchmarks\<BenchmarkProject>"
Remove-Item "$benchDir\bin\Release\net10.0\Job-*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$benchDir\BenchmarkDotNet.Artifacts\results\*" -Force -ErrorAction SilentlyContinue

# 4. Run benchmarks
cd $benchDir
dotnet run -c Release -f net10.0 -- --filter '*'
```

### Critical rules

1. **Always clean `Job-*` directories** before running. BDN's out-of-process toolchain creates `Job-*` subdirectories under `bin\Release\net10.0\`. Stale ones cause file locks; BDN's build exits with code 1 and **silently drops benchmarks** from results. You won't see an error — you'll just get fewer results.
2. **Never pipe BDN output through `Select-Object -First N`** or any truncating command. This kills the BDN host process mid-run, producing incomplete/corrupt results.
3. **Always pass `-- --filter '*'`** to run all benchmarks non-interactively. The `*` **must be single-quoted** in PowerShell to prevent glob expansion. Without quoting, PowerShell expands `*` to filenames, BDN receives no valid filter, and presents an interactive menu that blocks the shell.
4. **Detect completion by polling for result files, not by waiting on shell output.** BDN output buffers in PowerShell and `read_powershell` may return no new output even after the run finishes. Instead, poll for result files to detect completion:
   ```powershell
   Get-ChildItem "$benchDir\BenchmarkDotNet.Artifacts\results\*-report-default.md"
   ```
   Once the expected number of result files appear, the run is complete. Read results directly from those files.
5. **Use `mode="sync"` with `initial_wait=30`** when running from the Copilot shell. BDN typically runs for 15-30 minutes depending on the number of benchmarks. After initial_wait expires, the command continues in background. Poll for result files periodically rather than blocking on `read_powershell`.

### Result locations

- Results are at `benchmarks/<BenchmarkProject>/BenchmarkDotNet.Artifacts/results/` (**not** the repo root).
- Markdown reports: `*-report-default.md` files, one per benchmark class.
- JSON reports: `*-report-full.json` files, one per benchmark class.

### Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Fewer benchmarks than expected | Stale `Job-*` dirs caused build failure | Clean `Job-*` dirs and re-run |
| BDN build exits code 1 | File lock from prior run | Clean `Job-*` dirs |
| No source-generated methods in results | Source generator didn't run | Build in Release config, check `obj\Release\net10.0\generated\` for `.g.cs` files |
| Results in wrong directory | Looking at repo root | Check `benchmarks\...\BenchmarkDotNet.Artifacts\results\` |

### JSONata benchmarks

The `benchmarks/Corvus.Text.Json.Jsonata.Benchmarks/` project compares the JSONata **code generator (CG)** against the **runtime compiler (RT)** and a **Jsonata.Net.Native** baseline across 20 expression categories. There are 62 benchmarks total (20 CG + 20 RT + 22 Native). If results show fewer than 62, something went wrong — see troubleshooting above.

Build timeout is pre-configured in `Program.cs` at 15 minutes (`WithBuildTimeout(TimeSpan.FromMinutes(15))`). No `--buildTimeout` flag needed.

**Method naming convention:**
- `Corvus_<Category>` → RT (runtime compiler)
- `Corvus_CodeGen_<Category>` → CG (code generator)
- `Native_<Category>` → Jsonata.Net.Native baseline
- **CG/RT ratio** = `CodeGen.Mean / Corvus.Mean`. CG WIN ≤ 0.95, RT WIN ≥ 1.05, PARITY otherwise.

After running benchmarks, generate the full comparison table with:

```bash
node benchmarks/bench_table.js
```

This reads `*-report-default.md` files and outputs a markdown table with Native/RT/CG columns for Mean, Ratio, and Allocated. Flags benchmarks where CG or RT exceeds parity (ratio > 1.0).

### JSON Schema validation benchmarks

The `benchmarks/Corvus.Text.Json.Benchmarks/` project compares validation performance against a frozen baseline. The `--buildTimeout 1200` flag is required because the default 120s is too short for this solution with source generators.

## All benchmark projects (moved from copilot-instructions.md)

In addition to the JSON Schema validation benchmarks and JSONata benchmarks documented above, the following benchmark projects exist under `benchmarks/`:

| Project | What it benchmarks |
|---------|-------------------|
| `Corvus.Text.Json.Benchmarks` | JSON Schema validation (B/ vs C/ frozen baseline) |
| `Corvus.Text.Json.Jsonata.Benchmarks` | JSONata CG vs RT vs Jsonata.Net.Native |
| `Corvus.Text.Json.JMESPath.Benchmarks` | JMESPath performance |
| `Corvus.Text.Json.JsonLogic.Benchmarks` | JsonLogic performance |
| `Corvus.Text.Json.JsonPath.Benchmarks` | JSONPath performance vs JsonEverything |
| `Corvus.Text.Json.Yaml.Benchmarks` | YAML conversion performance |
| `Corvus.Numerics.Benchmarks` | BigNumber arithmetic performance |
| `Corvus.Json.Validator.Benchmarks` | Dynamic validator performance |
| `Corvus.Text.Json.CodeGeneration.Benchmarks` | Code generation pipeline performance |
| `Corvus.Text.Json.Benchmarks.Validation` | Standalone evaluator validation benchmarks |

All follow the same BDN rules documented in the "Running BenchmarkDotNet" section above.
