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

All test projects use **MSTest** (MSTest.Sdk 4.2.2) with **Microsoft Testing Platform** (MTP). The `global.json` pins the MSTest.Sdk version and configures MTP as the test runner.

All test projects target both `net10.0` and `net481`. `CodeGenerator.Tests` produces an empty assembly on net481 (the CLI tool is .NET 10 only); `<IsTestProject>false</IsTestProject>` on net481 plus `--ignore-exit-code 8` in CI prevents it from failing the test run. Three source-generator test projects (JMESPath, Jsonata, JsonLogic) exclude their `SourceGeneratorDiagnosticTests.cs` file on net481 because those Roslyn-hosted tests require .NET Core reference assemblies (integration tests run on both TFMs). Running `dotnet test --solution Corvus.Text.Json.slnx` without `-f` runs tests on all applicable TFMs.

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

1. **Warning-free build**: `dotnet build Corvus.Text.Json.slnx` must report `0 Warning(s)`.
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

### ⚠️ Prefer exact assertions

Always use `Assert.AreEqual` with the complete expected value. Do **not** use `StringAssert.Contains`, `Assert.IsTrue(x.StartsWith(...))`, or similar weak assertions for output verification — these mask bugs where the format changes but still contains the checked substring.

**Acceptable exceptions:**
- Error/exception message substring checks (e.g., `StringAssert.Contains(ex.Message, "T0410")`)
- Buffer-growth tests writing 15+ iterations in a loop (output is hundreds of bytes; Contains verifying data integrity is fine)
- Non-deterministic output that cannot be reproduced exactly

**Technique for capturing exact values:**
1. Write a temporary `Assert.Fail(actualValue)` or `Console.WriteLine` to capture the exact output
2. Or use a file-based app (`.cs` script) referencing the library project to call the API directly
3. Use raw string literals (`"""`) for JSON expected values — `\u002B`, `\n`, `\t` are literal characters matching JSON content

```csharp
// GOOD — exact assertion with raw string literal
Assert.AreEqual("""{"name":"Alice","age":30}""", json);

// BAD — weak assertion that passes even if output is wrong
StringAssert.Contains(json, "Alice");
```

```powershell
# Run all tests (standard — all 21 test projects, both TFMs)
dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop"

# Run all tests on a specific TFM
dotnet test --solution Corvus.Text.Json.slnx -f net10.0 --filter "TestCategory!=failing&TestCategory!=outerloop"

# Run a single test class
dotnet test --solution Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParsedJsonDocumentTests&TestCategory!=failing&TestCategory!=outerloop"

# Run a single test method
dotnet test --solution Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParseValidUtf8BOM&TestCategory!=failing&TestCategory!=outerloop"
```

### Test by Feature Area

```powershell
# JSON Schema draft-specific tests (all in Corvus.Text.Json.Tests)
dotnet test --project tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft202012&TestCategory!=failing&TestCategory!=outerloop"

# Standalone evaluator tests (all in Corvus.Text.Json.Tests)
dotnet test --project tests\Corvus.Text.Json.Tests --filter "TestCategory~StandaloneEvaluatorTestSuite&TestCategory!=failing&TestCategory!=outerloop"

# Annotation tests (all in Corvus.Text.Json.Tests)
dotnet test --project tests\Corvus.Text.Json.Tests --filter "TestCategory~AnnotationTestSuite&TestCategory!=failing&TestCategory!=outerloop"

# JSONata conformance
dotnet test --project tests\Corvus.Text.Json.Jsonata.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"

# JMESPath conformance
dotnet test --project tests\Corvus.Text.Json.JMESPath.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"

# YAML conformance
dotnet test --project tests\Corvus.Text.Json.Yaml.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"

# JSONPath conformance
dotnet test --project tests\Corvus.Text.Json.JsonPath.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"

# JSONPath code-gen
dotnet test --project tests\Corvus.Text.Json.JsonPath.CodeGeneration.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"
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
- **Tests**: `net10.0;net481` (all projects). `CodeGenerator.Tests` is an empty assembly on net481.
- Run a specific TFM: `dotnet test -f net10.0 ...`

## Collecting Code Coverage

Use `dotnet-coverage` (Microsoft Code Coverage), **not** Coverlet. Coverlet 10.0.0 has a known instrumentation bug that reports 0% for many types despite tests exercising the code.

### Full test suite coverage (all TFMs, merged automatically)

### ⚠️ CRITICAL: Always collect baseline/full coverage WITHOUT `-f`

**NEVER** pass `-f net10.0` when collecting baseline or full coverage. This misses all `#if !NET` / `#if NETSTANDARD2_0` code paths (unsafe pointer fallbacks, polyfills, etc.) and produces incomplete results. Omit `-f` entirely — both TFMs run and `dotnet-coverage` merges them automatically. Only use `-f` for targeted single-test-class verification during iterative improvement.

```powershell
# 1. Build once
dotnet build Corvus.Text.Json.slnx

# 2. Collect coverage — all TFMs (dotnet-coverage merges automatically)
dotnet-coverage collect `
    --output TestResults\coverage.cobertura.xml `
    --output-format cobertura `
    -s dotnet-coverage.settings.xml `
    "dotnet test --solution Corvus.Text.Json.slnx --filter `"TestCategory!=failing&TestCategory!=outerloop`" --no-build"
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
    "dotnet test --solution Corvus.Text.Json.slnx -f net10.0 --filter `"TestCategory!=failing&TestCategory!=outerloop`" --no-build"
```

Note: single-TFM runs miss TFM-conditional branches. Use the all-TFM approach above for accurate coverage baselines.

### Single test class coverage

```powershell
dotnet-coverage collect `
    --output TestResults\mytest.cobertura.xml `
    --output-format cobertura `
    -s dotnet-coverage.settings.xml `
    "dotnet test --solution Corvus.Text.Json.slnx -f net10.0 --filter `"FullyQualifiedName~MyTestClass&TestCategory!=failing&TestCategory!=outerloop`" --no-build"
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
       "dotnet test --solution Corvus.Text.Json.slnx --filter `"FullyQualifiedName~MyNewTestClass&TestCategory!=failing&TestCategory!=outerloop`" --no-build"
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
Running `dotnet test` without `TestCategory!=failing&TestCategory!=outerloop` will run tests that are expected to fail or are slow stress tests, producing misleading failures.

### Source generator not running
If generated types are missing, ensure you're building in the correct configuration. Check `obj\{Config}\{TFM}\generated\` for `.g.cs` files.

## Cross-References
- For benchmarks, see the `corvus-benchmarks` skill
- For code generation, see the `corvus-codegen` skill
- For test suite regeneration, see the `corvus-test-suite-regeneration` skill
- For full conventions, see `.github/copilot-instructions.md`

## Multi-TFM test-project quirks (moved from copilot-instructions.md)

All test projects target both `net10.0` and `net481`. `CodeGenerator.Tests` produces an empty assembly on net481 (the CLI tool it tests is .NET 10 only); `<IsTestProject>false</IsTestProject>` on net481 plus `--ignore-exit-code 8` in CI prevents the empty assembly from failing the test run. Three source-generator test projects (JMESPath, Jsonata, JsonLogic) exclude their `SourceGeneratorDiagnosticTests.cs` file on net481 because those Roslyn-hosted tests require .NET Core reference assemblies. Running `dotnet test --solution Corvus.Text.Json.slnx` without `-f` runs tests on all applicable TFMs.

## Converting UTF-8 bytes to strings in tests (moved from copilot-instructions.md)

When a method under test writes its output to a `Span<byte>` (i.e. a UTF-8 Utf8 format method), use `JsonReaderHelper.TranscodeHelper` to turn the result into a `string` for assertion. This is the standard cross-platform approach used throughout the test suite — it abstracts over the `#if NET` / `netstandard2.0` boundary so tests don't need `#if` blocks of their own.

```csharp
Span<byte> destination = stackalloc byte[100];

bool success = JsonElementHelpers.TryFormatCurrency(
    isNegative, integral, fractional, exponent,
    destination, out int bytesWritten, precision, formatInfo);

Assert.IsTrue(success);
string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
Assert.AreEqual(expected, result);
```

**Available overloads** (all in `Corvus.Text.Json.Reader.JsonReaderHelper`, internal):

| Signature | Use when |
|---|---|
| `string TranscodeHelper(ReadOnlySpan<byte>)` | You need a `string` from a UTF-8 buffer — most common in tests |
| `int TranscodeHelper(ReadOnlySpan<byte>, Span<char>)` | You already have a `char` destination buffer |
| `bool TryTranscode(ReadOnlySpan<byte>, Span<char>, out int)` | Non-throwing variant; returns `false` if the destination is too small |
| `int TranscodeHelper(ReadOnlySpan<char>, Span<byte>)` | Reverse direction: `char` → UTF-8 bytes |

On `net9.0`+ these delegate to `Encoding.UTF8.GetString(ReadOnlySpan<byte>)` and related span APIs. On `netstandard2.0` / `net481` they fall back to `unsafe fixed`-pointer overloads. Invalid UTF-8 always throws `InvalidOperationException` (wrapping `DecoderFallbackException`) rather than letting the raw codec exception escape.

## Coverage improvement methodology (moved from copilot-instructions.md)

- **Data-driven coverage improvement** — when working to improve code coverage, ONLY write tests that target specific uncovered branches/lines identified in Cobertura XML coverage reports. Never write generic tests for already-covered functions hoping they might help. The process is: (1) collect coverage **for ALL TFMs** (do NOT pass `-f net10.0` — omit `-f` entirely so both net10.0 and net481 run and merge automatically), (2) parse the Cobertura XML to find exact uncovered line ranges, (3) read the actual source code at those lines to understand the uncovered logic, (4) devise expressions/inputs that exercise those specific code paths, (5) verify with the reference implementation where applicable, (6) **after writing tests, re-collect coverage for just those tests and verify the specific target lines moved from 0 to >0 hits** — "tests pass" does NOT mean "target code paths exercised." If target lines are still uncovered, the tests are exercising different code paths and must be revised, (7) **iterate until every target line is covered, or you have verified evidence that a path is unreachable** — do not stop after one attempt. For lines you cannot cover, verify the claim by tracing all callers and checking generated code before reporting to the user; provide the evidence (e.g., "grep for `Source<TContext>` across all `.cs` files finds no call sites that construct one"). The coverage report is the sole source of truth for what needs testing — not guesswork about what "might" be uncovered. Remove any tests that do not contribute novel coverage.
- **Coverage tooling** — use `dotnet-coverage` (Microsoft Code Coverage), **not** Coverlet (`--collect:"XPlat Code Coverage"`). Coverlet 10.0.0 has a known instrumentation bug that reports 0% coverage for many types (including ref structs, static classes, and regular sealed classes) despite tests exercising the code. The repo includes `dotnet-coverage.settings.xml` which filters to published library assemblies and excludes non-actionable source files. **⚠️ CRITICAL: NEVER pass `-f net10.0` when collecting baseline or full coverage** — this misses all `#if !NET` / netstandard2.0 code paths. Omit `-f` entirely so both TFMs run and merge automatically: `dotnet-coverage collect --output result.cobertura.xml --output-format cobertura -s dotnet-coverage.settings.xml "dotnet test --solution Corvus.Text.Json.slnx --filter \"TestCategory!=failing&TestCategory!=outerloop\" --no-build"`. The output is a single Cobertura XML with merged multi-TFM coverage. Only use `-f net10.0` for single-test-class verification during iterative coverage improvement. See the `corvus-build-and-test` skill for XML parsing patterns and the coverage verification loop.
- **Coverage exclusions** — the `dotnet-coverage.settings.xml` `<Sources><Exclude>` section filters out non-actionable source files that inflate coverage denominators: (1) `src-v4/Corvus.Json.ExtendedTypes/Corvus.Json/GeneratedCoreTypes/` — V4 CLI-generated core types (~144 files), (2) all Roslyn source-generator output under `obj/` (matched by `.*\\obj\\.*\.g\.cs$`), (3) auto-generated resource files (`SR.cs` and `*.Designer.cs`), (4) `Corvus/Globalization/` — .NET runtime-derived IDN/Unicode polyfills (CharUnicodeInfoData lookup tables, IdnMapping); the data tables are auto-generated byte arrays indexed by Unicode code point ranges where individual elements are not meaningfully testable, and (5) `Internal/Unicode/` — .NET runtime-derived grapheme/text segmentation polyfills compiled only on netstandard2.0/net481. When parsing Cobertura XML to calculate coverage percentages, apply the same exclusions: skip classes whose `filename` matches any of these patterns. Failure to exclude these will significantly undercount coverage for packages with generated code or resource files.
