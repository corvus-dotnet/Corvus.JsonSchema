# Running Tests

This guide covers the test projects in the solution and how to run them.

## Quick start

```powershell
# Run the standard test suite (excludes slow stress tests and integration tests)
dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop&TestCategory!=integration"
```

Always exclude the `failing`, `outerloop`, and `integration` categories when running the full suite. The `outerloop` tests are long-running stress and thread-safety tests. The `integration` tests require Docker/Podman to run real broker containers.

### Integration Tests (Docker/Podman Required)

Integration tests for AsyncAPI transports require Docker or Podman to run real broker containers (NATS, Kafka, RabbitMQ, MQTT, Azure Service Bus).

**Prerequisites:**
- **Docker**: Works out of the box in CI (GitHub Actions ubuntu runners)
- **Podman on Windows**: Requires environment variable normalization

**Running integration tests locally with Podman:**

Use the helper script (recommended):

```powershell
.\run-integration-tests.ps1
```

Or manually normalize the environment variable:

```powershell
# Normalize DOCKER_HOST for Podman compatibility
$originalDockerHost = $env:DOCKER_HOST
if ($originalDockerHost -and $originalDockerHost.StartsWith("npipe:////")) {
    $env:DOCKER_HOST = "npipe://" + $originalDockerHost.Substring("npipe:////".Length)
    Write-Host "Normalized DOCKER_HOST from '$originalDockerHost' to '$env:DOCKER_HOST'"
}

# Run integration tests
dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory=integration&TestCategory!=failing"
```

**Why normalization is needed:** Podman on Windows uses `npipe:////./pipe/podman-machine-default` (4 slashes), but Docker.DotNet's NPipe handler expects `npipe://./pipe/name` (2 slashes). The normalization ensures compatibility with both Docker and Podman.

## Test projects

The solution contains twenty-one runnable test projects and six supporting model/utility projects.

### Runnable test projects

| Project | Frameworks | Description |
|---------|-----------|-------------|
| `Corvus.Text.Json.Tests` | net9.0, net10.0, net481 | Core library tests — parsing, writing, schema validation, mutable documents, migration equivalence |
| `Corvus.Text.Json.Validator.Tests` | net9.0, net10.0, net481 | Dynamic schema validator (runtime compilation) |
| `Corvus.Text.Json.CodeGenerator.Tests` | net10.0 | CLI code generator |
| `Corvus.Text.Json.Migration.Analyzers.Tests` | net10.0 | V4→V5 migration Roslyn analyzers and code fixes |
| `Corvus.Text.Json.Analyzers.Tests` | net10.0 | Roslyn analyzers for UTF-8 strings, unnecessary conversions, property naming |
| `Corvus.Numerics.Tests` | net9.0, net10.0, net481 | `BigNumber` / `BigInteger` arithmetic |
| `Corvus.Text.Json.Patch.Tests` | net10.0, net481 | RFC 6902 JSON Patch operations — official test suite |
| `Corvus.Text.Json.Yaml.Tests` | net10.0, net481 | YAML ↔ JSON conversion, `Utf8YamlWriter`, event parsing, YAML test suite conformance (Corvus.Text.Json variant) |
| `Corvus.Yaml.SystemTextJson.Tests` | net10.0, net481 | YAML ↔ JSON conversion — YAML test suite conformance (System.Text.Json-only variant) |
| `Corvus.Text.Json.Jsonata.Tests` | net10.0, net481 | JSONata runtime conformance — official test suite |
| `Corvus.Text.Json.Jsonata.CodeGeneration.Tests` | net10.0 | JSONata code generation conformance and edge-case tests |
| `Corvus.Text.Json.Jsonata.SourceGenerator.Tests` | net10.0 | JSONata source generator integration tests |
| `Corvus.Text.Json.JMESPath.Tests` | net10.0, net481 | JMESPath runtime conformance — official compliance test suite |
| `Corvus.Text.Json.JMESPath.CodeGeneration.Tests` | net10.0 | JMESPath code generation conformance |
| `Corvus.Text.Json.JMESPath.SourceGenerator.Tests` | net10.0 | JMESPath source generator integration tests |
| `Corvus.Text.Json.JsonLogic.Tests` | net10.0, net481 | JsonLogic runtime conformance — official test suite |
| `Corvus.Text.Json.JsonLogic.CodeGeneration.Tests` | net10.0 | JsonLogic code generation tests |
| `Corvus.Text.Json.JsonLogic.SourceGenerator.Tests` | net10.0 | JsonLogic source generator integration tests |
| `Corvus.Text.Json.JsonPath.Tests` | net10.0, net481 | JSONPath (RFC 9535) runtime conformance — official compliance test suite |
| `Corvus.Text.Json.JsonPath.CodeGeneration.Tests` | net10.0 | JSONPath code generation conformance |
| `Corvus.Text.Json.JsonPath.SourceGenerator.Tests` | net10.0 | JSONPath source generator integration tests |

### Supporting projects (not runnable)

| Project | Purpose |
|---------|---------|
| `Corvus.Text.Json.Tests.GeneratedModels` | Source-generated V5 model types used by the main test suite |
| `Corvus.Text.Json.Tests.GeneratedModels.OptionalAsNullable` | V5 models with `OptionalAsNullable` enabled |
| `Corvus.Text.Json.Tests.MigrationModels.V4` | V4-generated model types (references `src-v4/Corvus.Json.ExtendedTypes`) |
| `Corvus.Text.Json.Tests.MigrationModels.V5` | V5 equivalents of the V4 models |
| `Corvus.Text.Json.Tests.MigrationSchemas` | JSON schemas used to generate the migration model types |
| `Corvus.JsonSchemaTestSuite.CodeGenerator` | Utility that regenerates MSTest test classes from the JSON Schema Test Suite submodule |

## Running specific test areas

### All V5 library tests

```powershell
dotnet test --project tests\Corvus.Text.Json.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"
```

This is the main test suite covering the V5 library: reader, writer, parsed documents, mutable documents, schema validation, and generated type behaviour.

### Migration equivalence tests (V4 vs V5)

The `MigrationEquivalenceTests` folder within the main test project compares V4-generated and V5-generated types side-by-side for parsing, serialization, mutation, validation, and property access:

```powershell
dotnet test --project tests\Corvus.Text.Json.Tests --filter "FullyQualifiedName~MigrationEquivalence&TestCategory!=failing&TestCategory!=outerloop"
```

### Migration analyzer tests

```powershell
dotnet test --project tests\Corvus.Text.Json.Migration.Analyzers.Tests
```

These test the Roslyn analyzers (CVJ001–CVJ025) and their associated code fixes. No category filter needed — they run quickly.

### JSON Schema Test Suite

Tests are generated from the [JSON Schema Test Suite](https://github.com/json-schema-org/JSON-Schema-Test-Suite) submodule and tagged by draft:

```powershell
# All schema drafts
dotnet test --project tests\Corvus.Text.Json.Tests --filter "TestCategory~JsonSchemaTestSuite&TestCategory!=failing&TestCategory!=outerloop"

# A single draft
dotnet test --project tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft202012&TestCategory!=failing&TestCategory!=outerloop"
dotnet test --project tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft201909&TestCategory!=failing&TestCategory!=outerloop"
dotnet test --project tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft7&TestCategory!=failing&TestCategory!=outerloop"
dotnet test --project tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft6&TestCategory!=failing&TestCategory!=outerloop"
dotnet test --project tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft4&TestCategory!=failing&TestCategory!=outerloop"
```

### Numerics tests

```powershell
dotnet test --project tests\Corvus.Numerics.Tests
```

### Validator tests

```powershell
dotnet test --project tests\Corvus.Text.Json.Validator.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"
```

### Code generator tests

```powershell
dotnet test --project tests\Corvus.Text.Json.CodeGenerator.Tests
```

### JSONata tests

The JSONata test projects exercise the runtime evaluator, the code generator, and the source generator.

#### Runtime conformance (official test suite)

The `Corvus.Text.Json.Jsonata.Tests` project runs the full [JSONata test suite](https://github.com/jsonata-js/jsonata) against the interpreted evaluator. Each test case is loaded from the `Jsonata-Test-Suite/` submodule and exercises expression evaluation, error handling, variable bindings, recursion depth limits, and execution timeouts. Tests are tagged with the `testsuite` category.

```powershell
# All runtime conformance tests
dotnet test --project tests\Corvus.Text.Json.Jsonata.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"

# On a specific framework
dotnet test --project tests\Corvus.Text.Json.Jsonata.Tests -f net10.0 --filter "TestCategory!=failing&TestCategory!=outerloop"
dotnet test --project tests\Corvus.Text.Json.Jsonata.Tests -f net481 --filter "TestCategory!=failing&TestCategory!=outerloop"
```

Individual test cases have a 10-second timeout to catch runaway recursive expressions.

#### Code generation conformance

The `Corvus.Text.Json.Jsonata.CodeGeneration.Tests` project compiles every JSONata expression to C# using the code generator, then executes the generated code and compares results against the expected output. Tests are tagged with the `codegen-conformance` category.

```powershell
# All CG conformance tests
dotnet test --project tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests --filter "TestCategory=codegen-conformance"
```

A separate set of edge-case tests (tagged `codegen-edge`) exercises code paths that the conformance suite may not cover thoroughly — nested higher-order functions, lambda variable capture, parameter shadowing, and root-reference (`$$`) propagation:

```powershell
# CG edge-case tests only
dotnet test --project tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests --filter "TestCategory=codegen-edge"

# All CG tests (conformance + edge cases)
dotnet test --project tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests
```

#### Source generator integration

```powershell
dotnet test --project tests\Corvus.Text.Json.Jsonata.SourceGenerator.Tests
```

These tests verify that the Roslyn incremental source generator produces correct evaluators for representative expressions (property paths, arithmetic, string concatenation, aggregate functions, predicate filtering, higher-order functions).

### JsonLogic tests

The JsonLogic test projects mirror the JSONata structure: runtime conformance, code generation, and source generator integration.

#### Runtime conformance (official test suite)

The `Corvus.Text.Json.JsonLogic.Tests` project runs the full [official JsonLogic test suite](https://jsonlogic.com/tests.json) against the interpreted evaluator.

```powershell
dotnet test --project tests\Corvus.Text.Json.JsonLogic.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"
```

#### Code generation

```powershell
dotnet test --project tests\Corvus.Text.Json.JsonLogic.CodeGeneration.Tests
```

Tests that the code generator produces valid C# for representative JsonLogic rules (variable access, arithmetic, comparison, string concatenation, conditional logic).

#### Source generator integration

```powershell
dotnet test --project tests\Corvus.Text.Json.JsonLogic.SourceGenerator.Tests
```

Verifies that source-generated evaluators produce correct results for addition, conditional logic, string concatenation, array filtering, and missing-data checks.

### JSONPath tests

The JSONPath test projects exercise the runtime evaluator, the code generator, and the source generator against the [JSONPath Compliance Test Suite](https://github.com/jsonpath-standard/jsonpath-compliance-test-suite) (RFC 9535).

#### Runtime conformance (official test suite)

The `Corvus.Text.Json.JsonPath.Tests` project runs the full 723 official JSONPath compliance test cases against the interpreted evaluator.

```powershell
dotnet test --project tests\Corvus.Text.Json.JsonPath.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"
```

#### Code generation

```powershell
dotnet test --project tests\Corvus.Text.Json.JsonPath.CodeGeneration.Tests --filter "TestCategory!=failing&TestCategory!=outerloop"
```

Tests that the code generator produces valid C# for JSONPath expressions including property access, wildcards, array slices, filters, recursive descent, and custom function extensions.

#### Source generator integration

```powershell
dotnet test --project tests\Corvus.Text.Json.JsonPath.SourceGenerator.Tests
```

Verifies that source-generated evaluators produce correct results for representative JSONPath expressions.

## Running a single test class or method

```powershell
# Single class
dotnet test --solution Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParsedJsonDocumentTests&TestCategory!=failing&TestCategory!=outerloop"

# Single method (substring match)
dotnet test --solution Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParseValidUtf8BOM&TestCategory!=failing&TestCategory!=outerloop"
```

## Outerloop (stress) tests

The `[TestCategory("outerloop")]` attribute marks long-running stress and thread-safety tests. These are excluded from routine runs but should be run before major releases:

```powershell
dotnet test --project tests\Corvus.Text.Json.Tests --filter "TestCategory=outerloop"
```

## Near-outerloop tests and CI performance

Several tests in `Corvus.Text.Json.Tests` allocate very large buffers (200 MB–1 GB) but are **not** marked as `outerloop`. These "near-outerloop" tests have a significant impact on CI runtime and memory consumption.

### Impact analysis

Local profiling (May 2025) measured per-method wall-clock time across all `Utf8JsonWriterTests` methods. Three methods dominate:

| Method | Allocation | Cases | net10.0 | net481 | net481 penalty |
|--------|-----------|-------|---------|--------|---------------|
| `WriteLargeKeyEscapedValue` | `MaxUnescapedTokenSize/2` buffers | 90 | 115s | 277s | +162s |
| `WritingHugeBase64Bytes` | `new byte[1_000_000_000]` (1 GB) | 90 | 113s | 274s | +161s |
| `WritingTooLargeBase64Bytes` | `new byte[200_000_000]` (200 MB) | 90 | 43s | 117s | +74s |
| **All other methods (193)** | — | ~11,500 | 78s | 100s | +22s |

These three methods account for **98%** of the net481 test execution time penalty for the entire assembly. Each runs 90 times (the full `JsonOptions()` Cartesian product of indent character × indent size × new line × skip validation), even though the indentation options are irrelevant to the buffer management logic being tested.

### Memory pressure on CI

The `WritingHugeBase64Bytes` test allocates a 1 GB byte array on each of its 90 invocations. Although the GC reclaims the memory between runs, the peak working set can exceed 2 GB. When MTP runs test assemblies in parallel, this can overlap with other memory-intensive assemblies and trigger OOM kills — particularly on CI runners with limited available memory.

These tests (along with the other large-allocation writer and reader tests) are now categorized as `[TestCategory("outerloop")]` and excluded from CI via `--filter TestCategory!=outerloop`. With the outerloop tests excluded, the remaining test assemblies have modest memory footprints and can safely run in parallel without `--max-parallel-test-modules` throttling.

### Options for reducing impact

If CI time becomes a concern, these are the available levers (in order of impact):

1. **Reduce `JsonOptions()` to representative cases for outerloop methods** — instead of 90 option combinations, use 3–4 representative options (indented/not-indented, skip-validation/no-skip). This would reduce the three methods from 270 to ~12 total cases, saving most of the time while retaining the large-buffer coverage when outerloop tests are run locally.

2. **Run outerloop tests in a separate CI job** — a scheduled or manual workflow can run `--filter TestCategory=outerloop` on a runner with more memory, catching regressions without blocking PRs.

## Target framework selection

The main test projects multi-target `net9.0`, `net10.0`, and `net481`. To run against a specific framework:

```powershell
dotnet test --project tests\Corvus.Text.Json.Tests -f net10.0 --filter "TestCategory!=failing&TestCategory!=outerloop"
dotnet test --project tests\Corvus.Text.Json.Tests -f net481 --filter "TestCategory!=failing&TestCategory!=outerloop"
```

## Regenerating V5 migration model types

The `Corvus.Text.Json.Tests.MigrationModels.V5` project contains pre-generated V5 types from JSON schemas in `tests/Corvus.Text.Json.Tests.MigrationSchemas/`. These files are generated by the `corvusjson` CLI tool and committed to the repository.

When the code generator templates change (e.g. in `src/Corvus.Text.Json.CodeGeneration/`), the migration model types must be regenerated:

```powershell
# Build the code generator tool
dotnet build src\Corvus.Json.CodeGenerator -c Debug -f net10.0

$toolPath = "src\Corvus.Json.CodeGenerator\bin\Debug\net10.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe"
$outputPath = "tests\Corvus.Text.Json.Tests.MigrationModels.V5"
$schemaPath = "tests\Corvus.Text.Json.Tests.MigrationSchemas"

# Clear existing generated files
Get-ChildItem "$outputPath\*.cs" | Remove-Item -Force

# Regenerate from each schema
$schemas = @(
    @{ Schema = "migration-person.json";        RootType = "MigrationPerson" },
    @{ Schema = "migration-nested.json";        RootType = "MigrationNested" },
    @{ Schema = "migration-composite.json";     RootType = "MigrationComposite" },
    @{ Schema = "migration-item-array.json";    RootType = "MigrationItemArray" },
    @{ Schema = "migration-int-vector.json";    RootType = "MigrationIntVector" },
    @{ Schema = "migration-status-enum.json";   RootType = "MigrationStatusEnum" },
    @{ Schema = "migration-tuple.json";         RootType = "MigrationTuple" },
    @{ Schema = "migration-union.json";         RootType = "MigrationUnion" },
    @{ Schema = "migration-with-defaults.json"; RootType = "MigrationWithDefaults" }
)

foreach ($s in $schemas) {
    & $toolPath (Join-Path $schemaPath $s.Schema) `
        --rootNamespace "Corvus.Text.Json.Tests.MigrationModels.V5" `
        --outputPath $outputPath `
        --outputRootTypeName $s.RootType
}
```

After regenerating, rebuild and run the migration equivalence tests to verify:

```powershell
dotnet build tests\Corvus.Text.Json.Tests
dotnet test --project tests\Corvus.Text.Json.Tests --filter "FullyQualifiedName~MigrationEquivalenceTests"
```

## Regenerating JSON Schema Test Suite tests

The `JSON-Schema-Test-Suite/` directory is a git submodule. If you update it, **both** the V4 and V5 test generators must be re-run.

### Using the automated script (recommended)

The `update-json-schema-test-suite.ps1` script in the repository root handles the full workflow:

```powershell
.\update-json-schema-test-suite.ps1
```

This script:

1. Fetches and checks out the latest commit on the submodule's `main` branch
2. Deletes old generated test files (the generators only create files — they never delete stale ones)
3. Regenerates V5 test classes (type-based, standalone evaluator, and annotation)

To update to a different branch:

```powershell
.\update-json-schema-test-suite.ps1 -Branch some-branch
```

After the script completes, rebuild and run the tests to verify:

```powershell
dotnet build Corvus.Text.Json.slnx
dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop"
```

### Manual regeneration

If you need to run the generators individually, the details below explain each one.

#### V5 test generator

The `Corvus.JsonSchemaTestSuite.CodeGenerator` project reads the submodule and writes MSTest test classes into three output directories under `tests/Corvus.Text.Json.Tests/`:

- `JsonSchemaTestSuite/` — type-based tests
- `StandaloneEvaluatorTestSuite/` — standalone evaluator tests
- `AnnotationTestSuite/` — annotation tests

> **Important:** Delete existing `.cs` files from all three output directories before regenerating. The generator only creates files — it does not remove files that are no longer selected by the exclusion rules.

> **Important:** The generator must be run from its `bin/` directory (not via `dotnet run` from the repo root), because `appsettings.json` uses relative paths that resolve from the executable's working directory.

```powershell
# Build and run from bin directory
dotnet build tests\Corvus.JsonSchemaTestSuite.CodeGenerator -v q
cd tests\Corvus.JsonSchemaTestSuite.CodeGenerator\bin\Debug\net10.0
.\Corvus.JsonSchemaTestSuite.CodeGenerator.exe
cd ..\..\..\..\..\
```

**Configuration:** `tests/Corvus.JsonSchemaTestSuite.CodeGenerator/appsettings.json`

| Key | Purpose |
|-----|---------|
| `outputPath` | Relative path to the generated test output directory |
| `jsonSchemaTestSuite.baseDirectory` | Relative path to the `JSON-Schema-Test-Suite/` submodule |
| `jsonSchemaTestSuite.collections[]` | One entry per draft (draft4, draft6, draft7, draft2019-09, draft2020-12) |
| `collections[].name` | Draft name — becomes the MSTest `[TestCategory("JsonSchemaTestSuite", "...")]` value |
| `collections[].defaultVocabulary` | The `$schema` URI used for validation |
| `collections[].directory` | Subdirectory within the test suite (e.g. `tests/draft2020-12`) |
| `collections[].options[]` | Per-path overrides, e.g. `"validateFormat": true` for `optional/format` |
| `collections[].exclusions[]` | File-level or individual test-level exclusions with a `reason` string |

Each generated test class uses `TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(...)` at runtime to dynamically compile a V5 type from the schema, then validates test instances against it.

#### V4 tests

V4 schema validation tests live in `tests-v4/Corvus.Json.Specs.Tests/` as MSTest test classes. These test V4 types (`Corvus.Json.ExtendedTypes`, `Corvus.Json.CodeGeneration.CSharp`) using runtime Roslyn compilation via `JsonSchemaBuilderDriver`. The tests cover all drafts (4, 6, 7, 2019-09, 2020-12) plus OpenAPI 3.0 and are run as part of the main solution test pass.
