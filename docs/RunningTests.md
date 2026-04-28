# Running Tests

This guide covers the test projects in the solution and how to run them.

## Quick start

```powershell
# Run the standard test suite (excludes slow stress tests)
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"
```

Always exclude the `failing` and `outerloop` categories when running the full suite. The `outerloop` tests are long-running stress and thread-safety tests that are not suitable for routine development.

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
| `Corvus.JsonSchemaTestSuite.CodeGenerator` | Utility that regenerates xUnit test classes from the JSON Schema Test Suite submodule |

## Running specific test areas

### All V5 library tests

```powershell
dotnet test tests\Corvus.Text.Json.Tests --filter "category!=failing&category!=outerloop"
```

This is the main test suite covering the V5 library: reader, writer, parsed documents, mutable documents, schema validation, and generated type behaviour.

### Migration equivalence tests (V4 vs V5)

The `MigrationEquivalenceTests` folder within the main test project compares V4-generated and V5-generated types side-by-side for parsing, serialization, mutation, validation, and property access:

```powershell
dotnet test tests\Corvus.Text.Json.Tests --filter "FullyQualifiedName~MigrationEquivalence&category!=failing&category!=outerloop"
```

### Migration analyzer tests

```powershell
dotnet test tests\Corvus.Text.Json.Migration.Analyzers.Tests
```

These test the Roslyn analyzers (CVJ001–CVJ025) and their associated code fixes. No category filter needed — they run quickly.

### JSON Schema Test Suite

Tests are generated from the [JSON Schema Test Suite](https://github.com/json-schema-org/JSON-Schema-Test-Suite) submodule and tagged by draft:

```powershell
# All schema drafts
dotnet test tests\Corvus.Text.Json.Tests --filter "Trait~JsonSchemaTestSuite&category!=failing&category!=outerloop"

# A single draft
dotnet test tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft202012&category!=failing&category!=outerloop"
dotnet test tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft201909&category!=failing&category!=outerloop"
dotnet test tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft7&category!=failing&category!=outerloop"
dotnet test tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft6&category!=failing&category!=outerloop"
dotnet test tests\Corvus.Text.Json.Tests --filter "JsonSchemaTestSuite=Draft4&category!=failing&category!=outerloop"
```

### Numerics tests

```powershell
dotnet test tests\Corvus.Numerics.Tests
```

### Validator tests

```powershell
dotnet test tests\Corvus.Text.Json.Validator.Tests --filter "category!=failing&category!=outerloop"
```

### Code generator tests

```powershell
dotnet test tests\Corvus.Text.Json.CodeGenerator.Tests
```

### JSONata tests

The JSONata test projects exercise the runtime evaluator, the code generator, and the source generator.

#### Runtime conformance (official test suite)

The `Corvus.Text.Json.Jsonata.Tests` project runs the full [JSONata test suite](https://github.com/jsonata-js/jsonata) against the interpreted evaluator. Each test case is loaded from the `Jsonata-Test-Suite/` submodule and exercises expression evaluation, error handling, variable bindings, recursion depth limits, and execution timeouts. Tests are tagged with the `testsuite` category.

```powershell
# All runtime conformance tests
dotnet test tests\Corvus.Text.Json.Jsonata.Tests --filter "category!=failing&category!=outerloop"

# On a specific framework
dotnet test tests\Corvus.Text.Json.Jsonata.Tests -f net10.0 --filter "category!=failing&category!=outerloop"
dotnet test tests\Corvus.Text.Json.Jsonata.Tests -f net481 --filter "category!=failing&category!=outerloop"
```

Individual test cases have a 10-second timeout to catch runaway recursive expressions.

#### Code generation conformance

The `Corvus.Text.Json.Jsonata.CodeGeneration.Tests` project compiles every JSONata expression to C# using the code generator, then executes the generated code and compares results against the expected output. Tests are tagged with the `codegen-conformance` category.

```powershell
# All CG conformance tests
dotnet test tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests --filter "category=codegen-conformance"
```

A separate set of edge-case tests (tagged `codegen-edge`) exercises code paths that the conformance suite may not cover thoroughly — nested higher-order functions, lambda variable capture, parameter shadowing, and root-reference (`$$`) propagation:

```powershell
# CG edge-case tests only
dotnet test tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests --filter "category=codegen-edge"

# All CG tests (conformance + edge cases)
dotnet test tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests
```

#### Source generator integration

```powershell
dotnet test tests\Corvus.Text.Json.Jsonata.SourceGenerator.Tests
```

These tests verify that the Roslyn incremental source generator produces correct evaluators for representative expressions (property paths, arithmetic, string concatenation, aggregate functions, predicate filtering, higher-order functions).

### JsonLogic tests

The JsonLogic test projects mirror the JSONata structure: runtime conformance, code generation, and source generator integration.

#### Runtime conformance (official test suite)

The `Corvus.Text.Json.JsonLogic.Tests` project runs the full [official JsonLogic test suite](https://jsonlogic.com/tests.json) against the interpreted evaluator.

```powershell
dotnet test tests\Corvus.Text.Json.JsonLogic.Tests --filter "category!=failing&category!=outerloop"
```

#### Code generation

```powershell
dotnet test tests\Corvus.Text.Json.JsonLogic.CodeGeneration.Tests
```

Tests that the code generator produces valid C# for representative JsonLogic rules (variable access, arithmetic, comparison, string concatenation, conditional logic).

#### Source generator integration

```powershell
dotnet test tests\Corvus.Text.Json.JsonLogic.SourceGenerator.Tests
```

Verifies that source-generated evaluators produce correct results for addition, conditional logic, string concatenation, array filtering, and missing-data checks.

### JSONPath tests

The JSONPath test projects exercise the runtime evaluator, the code generator, and the source generator against the [JSONPath Compliance Test Suite](https://github.com/jsonpath-standard/jsonpath-compliance-test-suite) (RFC 9535).

#### Runtime conformance (official test suite)

The `Corvus.Text.Json.JsonPath.Tests` project runs the full 723 official JSONPath compliance test cases against the interpreted evaluator.

```powershell
dotnet test tests\Corvus.Text.Json.JsonPath.Tests --filter "category!=failing&category!=outerloop"
```

#### Code generation

```powershell
dotnet test tests\Corvus.Text.Json.JsonPath.CodeGeneration.Tests --filter "category!=failing&category!=outerloop"
```

Tests that the code generator produces valid C# for JSONPath expressions including property access, wildcards, array slices, filters, recursive descent, and custom function extensions.

#### Source generator integration

```powershell
dotnet test tests\Corvus.Text.Json.JsonPath.SourceGenerator.Tests
```

Verifies that source-generated evaluators produce correct results for representative JSONPath expressions.

## Running a single test class or method

```powershell
# Single class
dotnet test Corvus.Text.Json.Test.slnx --filter "FullyQualifiedName~ParsedJsonDocumentTests&category!=failing&category!=outerloop"

# Single method (substring match)
dotnet test Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParseValidUtf8BOM&category!=failing&category!=outerloop"
```

## Outerloop (stress) tests

The `[OuterLoop]` attribute (from `Microsoft.DotNet.XUnitExtensions`) marks long-running stress and thread-safety tests. These are excluded from routine runs but should be run before major releases:

```powershell
dotnet test tests\Corvus.Text.Json.Tests --filter "Category=outerloop"
```

## Target framework selection

The main test projects multi-target `net9.0`, `net10.0`, and `net481`. To run against a specific framework:

```powershell
dotnet test tests\Corvus.Text.Json.Tests -f net10.0 --filter "category!=failing&category!=outerloop"
dotnet test tests\Corvus.Text.Json.Tests -f net481 --filter "category!=failing&category!=outerloop"
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
dotnet test tests\Corvus.Text.Json.Tests --filter "FullyQualifiedName~MigrationEquivalenceTests"
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
4. Regenerates V4 spec feature files using **both** selectors (JsonSchema from `JSON-Schema-Test-Suite/` and OpenApi30 from `OpenApi-Test-Suite/`)

To update to a different branch:

```powershell
.\update-json-schema-test-suite.ps1 -Branch some-branch
```

After the script completes, rebuild and run the tests to verify:

```powershell
dotnet build Corvus.Text.Json.slnx
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"
```

### Manual regeneration

If you need to run the generators individually, the details below explain each one.

#### V5 test generator

The `Corvus.JsonSchemaTestSuite.CodeGenerator` project reads the submodule and writes xUnit test classes into three output directories under `tests/Corvus.Text.Json.Tests/`:

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
| `collections[].name` | Draft name — becomes the xUnit `[Trait("JsonSchemaTestSuite", "...")]` value |
| `collections[].defaultVocabulary` | The `$schema` URI used for validation |
| `collections[].directory` | Subdirectory within the test suite (e.g. `tests/draft2020-12`) |
| `collections[].options[]` | Per-path overrides, e.g. `"validateFormat": true` for `optional/format` |
| `collections[].exclusions[]` | File-level or individual test-level exclusions with a `reason` string |

Each generated test class uses `TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(...)` at runtime to dynamically compile a V5 type from the schema, then validates test instances against it.

#### V4 specs generator

The `Corvus.JsonSchema.SpecGenerator` project (in `src-v4/`) generates SpecFlow `.feature` files into `src-v4/Corvus.Json.Specs/Features/JsonSchema/`.

> **Important:** Delete existing `.feature` files from the output directory before regenerating.

> **Important:** The V4 generator requires **two separate runs** with different selectors and input directories:
> - `JsonSchemaOrgTestSuiteSelector.jsonc` — reads from the `JSON-Schema-Test-Suite/` submodule (drafts 4, 6, 7, 2019-09, 2020-12)
> - `OpenApiTestSuiteSelector.jsonc` — reads from the `OpenApi-Test-Suite/` directory (OpenApi30)
>
> Missing either run will silently drop features.

```powershell
dotnet build src-v4\Corvus.JsonSchema.SpecGenerator -c Release -v q

$exe = "src-v4\Corvus.JsonSchema.SpecGenerator\bin\Release\net8.0\Corvus.JsonSchema.SpecGenerator.exe"
$outputDir = "src-v4\Corvus.Json.Specs\Features\JsonSchema"

# Run with JsonSchema selector
& $exe JSON-Schema-Test-Suite $outputDir JsonSchemaOrgTestSuiteSelector.jsonc

# Run with OpenApi selector
& $exe OpenApi-Test-Suite $outputDir OpenApiTestSuiteSelector.jsonc
```

The three positional arguments are:

| Argument | Purpose |
|----------|---------|
| `inputDir` | Path to the test suite directory (`JSON-Schema-Test-Suite/` or `OpenApi-Test-Suite/`) |
| `outputDir` | Path to the generated `.feature` file output directory |
| `testselector` | Path to a JSONC selector file controlling which drafts, files, and individual tests to include/exclude |

**Configuration:** `src-v4/Corvus.JsonSchema.SpecGenerator/JsonSchemaOrgTestSuiteSelector.jsonc`

The selector file uses a recursive directory-matching structure:

| Key | Purpose |
|-----|---------|
| `subdirectories.<name>` | Matches a directory in the test suite (e.g. `draft2020-12`) |
| `.testSet` | Names the test set — used as the Gherkin scenario tag |
| `.outputFolder` | Subfolder within the output directory (e.g. `Draft2020212`) |
| `.assertFormat` | Whether format validation is enforced for this draft |
| `.excludeFromThisDirectory[]` | Regex patterns for files to skip (e.g. `"bignum\\.json"`) |
| `.testExclusions.<file>.<scenario>.testsToIgnoreIndices[]` | Individual test indices to skip within a scenario (e.g. leap-second tests) |
