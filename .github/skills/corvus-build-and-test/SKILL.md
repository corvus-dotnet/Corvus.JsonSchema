---
name: corvus-build-and-test
description: >
  Build, test, and run the Corvus.JsonSchema solution correctly. Covers multi-targeting
  (net9.0/net10.0/net481/netstandard2.0), mandatory test category filters, solution file
  selection, running specific test classes or methods, writing new tests, and diagnosing
  common build/test failures. USE FOR: building the solution, running tests, writing new
  test files, diagnosing test failures, understanding TFM targeting, finding the right
  test project for a feature area. DO NOT USE FOR: benchmark execution (use
  corvus-benchmarks), code generation (use corvus-codegen), test suite regeneration
  (use corvus-test-suite-regeneration).
---

# Building and Testing Corvus.JsonSchema

## Solution Files

| Solution | Purpose |
|----------|---------|
| `Corvus.Text.Json.slnx` | Main V5 solution — all libraries + all tests (use for `dotnet build` and `dotnet test`) |
| `Corvus.Text.Json.Benchmarks.slnx` | Benchmark projects only |

All test projects target both `net10.0` and `net481`. Projects with .NET Core-only dependencies (Analyzers, CodeGenerator, 4× CodeGeneration tests) use `<Compile Remove="**\*.cs">` on net481 to produce empty assemblies with 0 tests. Source generator test projects exclude only their diagnostic test files on net481 (integration tests run on both TFMs). This means `dotnet test Corvus.Text.Json.slnx` without `-f` runs tests on both TFMs.

V4 projects live in `src-v4/` and `tests-v4/` and are included in the solution.

## Build Commands

```powershell
# Full build
dotnet build Corvus.Text.Json.slnx

# Build a specific project
dotnet build src\Corvus.Text.Json\Corvus.Text.Json.csproj
```

`TreatWarningsAsErrors=true` is set across all projects — any warning fails the build.

## Pre-Commit Checks

Before every commit, verify these mandatory gates:

1. **Warning-free build**: `dotnet build Corvus.Text.Json.slnx -v q --nologo` must report `0 Warning(s)`.
2. **Code sample catalog**: if any file under `.github/`, `docs/`, or skill/instruction files was modified (even incidentally), update and verify:

```powershell
.\docs\update-code-sample-catalog.ps1 -UpdateFile <relative-path>   # for each changed file
.\docs\update-code-sample-catalog.ps1 -Check                         # must exit 0
```

CI runs the catalog check and fails the build if it is stale. This is the most commonly missed pre-commit gate.

## Running Tests

### Mandatory Filters

**ALWAYS** exclude `failing` and `outerloop` categories:

### ⚠️ Use Corvus types, not `System.Text.Json`

Test code and assertions must use `Corvus.Text.Json` types (`JsonElement`, `JsonValueKind`, `ParsedJsonDocument<T>`, etc.), **not** `System.Text.Json` equivalents. Do not add `using System.Text.Json;` to test files. The two namespaces share type names (`JsonElement`, `Utf8JsonWriter`, `JsonWriterOptions`) which cause ambiguity errors, and using STJ types in assertions would test the wrong library.

`System.Text.Json` is acceptable **only** for test data infrastructure — e.g., reading JSON fixture files with `System.Text.Json.JsonDocument` to enumerate test cases. In those cases, fully-qualify the STJ types (e.g., `System.Text.Json.JsonElement`).

```powershell
# Run all tests (standard — all 21 test projects, both TFMs)
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"

# Run all tests on a specific TFM
dotnet test Corvus.Text.Json.slnx -f net10.0 --filter "category!=failing&category!=outerloop"

# Run a single test class
dotnet test Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParsedJsonDocumentTests&category!=failing&category!=outerloop"

# Run a single test method
dotnet test Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParseValidUtf8BOM&category!=failing&category!=outerloop"
```

### Test by Feature Area

```powershell
# JSON Schema draft-specific tests (all in Corvus.Text.Json.Tests)
dotnet test tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft202012&category!=failing&category!=outerloop"

# Standalone evaluator tests (all in Corvus.Text.Json.Tests)
dotnet test tests\Corvus.Text.Json.Tests --filter "Trait~StandaloneEvaluatorTestSuite&category!=failing&category!=outerloop"

# Annotation tests (all in Corvus.Text.Json.Tests)
dotnet test tests\Corvus.Text.Json.Tests --filter "Trait~AnnotationTestSuite&category!=failing&category!=outerloop"

# JSONata conformance
dotnet test tests\Corvus.Text.Json.Jsonata.Tests --filter "category!=failing&category!=outerloop"

# JMESPath conformance
dotnet test tests\Corvus.Text.Json.JMESPath.Tests --filter "category!=failing&category!=outerloop"

# YAML conformance
dotnet test tests\Corvus.Text.Json.Yaml.Tests --filter "category!=failing&category!=outerloop"

# JSONPath conformance
dotnet test tests\Corvus.Text.Json.JsonPath.Tests --filter "category!=failing&category!=outerloop"

# JSONPath code-gen
dotnet test tests\Corvus.Text.Json.JsonPath.CodeGeneration.Tests --filter "category!=failing&category!=outerloop"
```

### Key Test Projects (21 runnable)

| Project | Tests |
|---------|-------|
| `Corvus.Text.Json.Tests` | Core library: parsing, mutation, schema validation, JSON Schema Test Suite (all drafts via `JsonSchemaTestSuite/`), standalone evaluator (`StandaloneEvaluatorTestSuite/`), and annotation collection (`AnnotationTestSuite/`) |
| `Corvus.Text.Json.Validator.Tests` | Dynamic schema validator (runtime compilation) |
| `Corvus.Text.Json.Jsonata.Tests` | JSONata runtime conformance |
| `Corvus.Text.Json.Jsonata.CodeGeneration.Tests` | JSONata code generation |
| `Corvus.Text.Json.Jsonata.SourceGenerator.Tests` | JSONata source generator integration |
| `Corvus.Text.Json.JMESPath.Tests` | JMESPath runtime conformance |
| `Corvus.Text.Json.JMESPath.CodeGeneration.Tests` | JMESPath code generation |
| `Corvus.Text.Json.JMESPath.SourceGenerator.Tests` | JMESPath source generator integration |
| `Corvus.Text.Json.JsonPath.Tests` | JSONPath (RFC 9535) runtime conformance |
| `Corvus.Text.Json.JsonPath.CodeGeneration.Tests` | JSONPath code generation |
| `Corvus.Text.Json.JsonPath.SourceGenerator.Tests` | JSONPath source generator integration |
| `Corvus.Text.Json.Yaml.Tests` | YAML conformance |
| `Corvus.Yaml.SystemTextJson.Tests` | YAML ↔ JSON (System.Text.Json-only variant) |
| `Corvus.Text.Json.JsonLogic.Tests` | JsonLogic runtime |
| `Corvus.Text.Json.JsonLogic.CodeGeneration.Tests` | JsonLogic code generation |
| `Corvus.Text.Json.JsonLogic.SourceGenerator.Tests` | JsonLogic source generator integration |
| `Corvus.Numerics.Tests` | BigNumber / BigInteger arithmetic |
| `Corvus.Text.Json.Patch.Tests` | RFC 6902 JSON Patch |
| `Corvus.Text.Json.CodeGenerator.Tests` | CLI code generator |
| `Corvus.Text.Json.Migration.Analyzers.Tests` | V4→V5 migration analyzers |
| `Corvus.Text.Json.Analyzers.Tests` | Roslyn analyzers |

Plus 6 supporting model/utility projects that generate types consumed by other tests.

## Target Frameworks

- **Libraries**: `net9.0;net10.0;netstandard2.0;netstandard2.1`
- **Tests**: `net9.0;net10.0;net481` (varies by project)
- Run a specific TFM: `dotnet test -f net10.0 ...`

## Collecting Code Coverage

Use `dotnet-coverage` (Microsoft Code Coverage), **not** Coverlet. Coverlet 10.0.0 has a known instrumentation bug that reports 0% for many types despite tests exercising the code.

### Full test suite coverage (all TFMs, merged automatically)

```powershell
# 1. Build once
dotnet build Corvus.Text.Json.slnx

# 2. Collect coverage — all TFMs (dotnet-coverage merges automatically)
dotnet-coverage collect `
    --output TestResults\coverage.cobertura.xml `
    --output-format cobertura `
    -s dotnet-coverage.settings.xml `
    "dotnet test Corvus.Text.Json.slnx --filter `"category!=failing&category!=outerloop`" --no-build -v q --nologo"
```

All test projects target both net10.0 and net481. Running without `-f` executes tests on both TFMs, and `dotnet-coverage` produces a single merged Cobertura XML. This captures TFM-conditional code paths (e.g., `#if NETSTANDARD2_0` polyfill branches, `net481` fallback code) that a single-TFM run would miss.

Full suite coverage runs ~150K tests across 21 test projects × 2 TFMs and takes 30–45 minutes.

### Single-TFM coverage (when needed)

For targeted debugging, you can collect coverage for a single TFM:

```powershell
dotnet-coverage collect `
    --output TestResults\coverage-net10.0.cobertura.xml `
    --output-format cobertura `
    -s dotnet-coverage.settings.xml `
    "dotnet test Corvus.Text.Json.slnx -f net10.0 --filter `"category!=failing&category!=outerloop`" --no-build -v q --nologo"
```

Note: single-TFM runs miss TFM-conditional branches. Use the all-TFM approach above for accurate coverage baselines.

### Single test class coverage

```powershell
dotnet-coverage collect `
    --output TestResults\mytest.cobertura.xml `
    --output-format cobertura `
    -s dotnet-coverage.settings.xml `
    "dotnet test Corvus.Text.Json.slnx -f net10.0 --filter `"FullyQualifiedName~MyTestClass&category!=failing&category!=outerloop`" --no-build -v q --nologo"
```

### Key points

- The `dotnet-coverage.settings.xml` in the repo root filters coverage to published library assemblies only (18 assemblies including Corvus.Numerics)
- Output is a single Cobertura XML — running without `-f` automatically merges both TFMs
- Always build before collecting: `dotnet build Corvus.Text.Json.slnx` first, then `--no-build` in the test command
- When comparing before/after, always use the same approach (preferably all-TFM)
- The Cobertura XML uses full Windows paths in `filename` attributes — use `os.path.basename()` or equivalent when parsing

### ⚠️ Do NOT use Coverlet

`--collect:"XPlat Code Coverage"` (Coverlet) reports 0% coverage for many types including ref structs, static classes, and even regular sealed classes. This was verified by running the same tests with both tools — `dotnet-coverage` correctly reported 65–92% coverage for types that Coverlet reported as 0%.

### Coverage settings file

The `dotnet-coverage.settings.xml` file controls which assemblies are instrumented and which source files are excluded. It includes all published V5 library assemblies plus V4 code generation assemblies. If you add a new published assembly, add a corresponding `<ModulePath>` entry.

**Source exclusions** configured in the settings file:
- `src-v4/Corvus.Json.ExtendedTypes/Corvus.Json/GeneratedCoreTypes/` — V4 CLI-generated core types (~144 files) that inflate the denominator without meaningful coverage value
- `*.g.cs` files under `obj/` — Roslyn source-generator output (regex generators, JSON schema generators, etc.)
- `SR.cs` and `*.Designer.cs` — auto-generated resource string files that are not meaningfully testable

When parsing Cobertura XML manually, apply the same exclusions: skip `<class>` entries whose `filename` contains `GeneratedCoreTypes`, has an `obj` directory segment, ends with `SR.cs`, or ends with `.Designer.cs`. Failure to exclude these will significantly undercount coverage for packages with resource files (e.g., Validator has 130 untestable lines in SR.cs + Strings.Designer.cs).

### Parsing Cobertura XML

The Cobertura XML has `<class>` elements inside `<package>` elements. Each `<class>` has a `filename` attribute and `<line>` children with `number`, `hits`, and optional `condition-coverage` attributes.

**Important:** Partial classes and compiler-generated closures (`<>c`) appear as separate `<class>` entries for the same file. When computing per-file coverage, aggregate across all `<class>` entries that share the same `filename`:

```python
import xml.etree.ElementTree as ET, os

def get_coverage_by_file(xmlfile):
    tree = ET.parse(xmlfile)
    root = tree.getroot()
    results = {}
    for cls in root.iter('class'):
        fn = cls.get('filename', '')
        basename = os.path.basename(fn)
        if basename not in results:
            results[basename] = {'covered': set(), 'total': set()}
        for l in cls.findall('.//line'):
            num = int(l.get('number', 0))
            results[basename]['total'].add(num)
            if int(l.get('hits', 0)) > 0:
                results[basename]['covered'].add(num)
    return results
```

Use sets (not counts) to avoid double-counting lines that appear in multiple `<class>` entries.

### Coverage verification loop

When writing tests to close coverage gaps, **always verify that the target lines are actually covered** — "tests pass" does NOT mean "target code paths exercised." Iterate until every target line is covered or you have verified evidence that a path is unreachable. Remove any tests that do not contribute novel coverage.

1. **Before writing tests:** Note the exact uncovered line numbers from the Cobertura XML
2. **Write tests** that you believe exercise those lines
3. **Run the tests** — confirm they pass
4. **Re-collect coverage** for just the new test class:
   ```powershell
   dotnet-coverage collect `
       --output TestResults\verify.cobertura.xml `
       --output-format cobertura `
       -s dotnet-coverage.settings.xml `
       "dotnet test Corvus.Text.Json.slnx --filter `"FullyQualifiedName~MyNewTestClass&category!=failing&category!=outerloop`" --no-build -v q --nologo"
   ```
5. **Parse the report** and check whether the specific target lines now have `hits > 0`
6. **If target lines are still at 0:** the tests exercise different code paths. Revise and repeat from step 2
7. **If a path appears unreachable:** verify the claim by tracing all callers and checking generated code before reporting to the user. Provide evidence (e.g., "grep for `Source<TContext>` across all `.cs` files finds zero call sites"). Do not assert unreachability without proof
8. **Remove redundant tests** — any test that contributes zero novel lines over the baseline must be deleted

Common pitfalls that cause this mismatch:
- Testing `SetProperty<TContext>(name, context, delegate)` exercises the delegate overload, NOT `Source<TContext>` — those are separate code paths
- JSON Patch copy operations where source is inside the destination array may not trigger overlap detection branches if the internal row layout doesn't straddle the insertion point
- Generated types have their own `Source<TContext>` that delegates to the base `JsonElement.Source<TContext>` — test through the generated type's `CreateBuilder<TContext>` to cover the base type

## Common Pitfalls

### Stale bin directories
Building individual `.csproj` files produces output in `bin\{TFM}\` (no config subfolder). Building via `.slnx` produces `bin\{Config}\{TFM}\`. Stale `bin\{TFM}\` directories cause test failures because relative paths in `appsettings.json` resolve incorrectly. Fix: always use `-c Debug` or `-c Release` explicitly, and delete stale `bin\{TFM}\` dirs.

### Missing test filter
Running `dotnet test` without `category!=failing&category!=outerloop` will run tests that are expected to fail or are slow stress tests, producing misleading failures.

### Source generator not running
If generated types are missing, ensure you're building in the correct configuration. Check `obj\{Config}\{TFM}\generated\` for `.g.cs` files.

## Cross-References
- For benchmarks, see the `corvus-benchmarks` skill
- For code generation, see the `corvus-codegen` skill
- For test suite regeneration, see the `corvus-test-suite-regeneration` skill
- For full conventions, see `.github/copilot-instructions.md`
