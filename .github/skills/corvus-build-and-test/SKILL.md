---
name: corvus-build-and-test
description: >
  Build, test, and run the Corvus.JsonSchema solution correctly. Covers multi-targeting
  (net9.0/net10.0/net481/netstandard2.0), mandatory test category filters, solution file
  selection, running specific test classes or methods, and diagnosing common build/test
  failures. USE FOR: building the solution, running tests, diagnosing test failures,
  understanding TFM targeting, finding the right test project for a feature area.
  DO NOT USE FOR: benchmark execution (use corvus-benchmarks), code generation
  (use corvus-codegen), test suite regeneration (use corvus-test-suite-regeneration).
---

# Building and Testing Corvus.JsonSchema

## Solution Files

| Solution | Purpose |
|----------|---------|
| `Corvus.Text.Json.slnx` | Main V5 solution — libraries + tests (use for `dotnet build`) |
| `Corvus.Text.Json.Test.slnx` | Tests only (use for `dotnet test`) |
| `Corvus.Text.Json.Benchmarks.slnx` | Benchmark projects only |

V4 projects live in `src-v4/` and `tests-v4/` and are included in the main solution.

## Build Commands

```powershell
# Full build
dotnet build Corvus.Text.Json.slnx

# Build a specific project
dotnet build src\Corvus.Text.Json\Corvus.Text.Json.csproj
```

`TreatWarningsAsErrors=true` is set across all projects — any warning fails the build.

## Running Tests

### Mandatory Filters

**ALWAYS** exclude `failing` and `outerloop` categories:

```powershell
# Run all tests (standard)
dotnet test Corvus.Text.Json.Test.slnx --filter "category!=failing&category!=outerloop"

# Run a single test class
dotnet test Corvus.Text.Json.Test.slnx --filter "FullyQualifiedName~ParsedJsonDocumentTests&category!=failing&category!=outerloop"

# Run a single test method
dotnet test Corvus.Text.Json.Test.slnx --filter "FullyQualifiedName~ParseValidUtf8BOM&category!=failing&category!=outerloop"
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

### Full test suite coverage

```powershell
dotnet-coverage collect `
    --output TestResults\coverage.cobertura.xml `
    --output-format cobertura `
    -s dotnet-coverage.settings.xml `
    "dotnet test Corvus.Text.Json.Test.slnx -f net10.0 --filter `"category!=failing&category!=outerloop`" --no-build -v q --nologo"
```

### Single test class coverage

```powershell
dotnet-coverage collect `
    --output TestResults\mytest.cobertura.xml `
    --output-format cobertura `
    -s dotnet-coverage.settings.xml `
    "dotnet test Corvus.Text.Json.Test.slnx -f net10.0 --filter `"FullyQualifiedName~MyTestClass&category!=failing&category!=outerloop`" --no-build -v q --nologo"
```

**Key points:**
- The `dotnet-coverage.settings.xml` in the repo root filters coverage to core library assemblies only
- Output is a single Cobertura XML — no merge step is needed (unlike Coverlet which produces one report per test project)
- Always build before collecting: `dotnet build Corvus.Text.Json.slnx` first, then `--no-build` in the test command
- Full suite coverage takes ~10 minutes (68K+ tests)

### ⚠️ Do NOT use Coverlet

`--collect:"XPlat Code Coverage"` (Coverlet) reports 0% coverage for many types including ref structs, static classes, and even regular sealed classes. This was verified by running the same tests with both tools — `dotnet-coverage` correctly reported 65–92% coverage for types that Coverlet reported as 0%.

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
