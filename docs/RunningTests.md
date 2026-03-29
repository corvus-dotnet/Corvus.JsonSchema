# Running Tests

This guide covers the test projects in the solution and how to run them.

## Quick start

```powershell
# Run the standard test suite (excludes slow stress tests)
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"
```

Always exclude the `failing` and `outerloop` categories when running the full suite. The `outerloop` tests are long-running stress and thread-safety tests that are not suitable for routine development.

## Test projects

The solution contains five runnable test projects and four supporting model/utility projects.

### Runnable test projects

| Project | Frameworks | Description |
|---------|-----------|-------------|
| `Corvus.Text.Json.Tests` | net9.0, net10.0, net481 | Core library tests — parsing, writing, schema validation, mutable documents, migration equivalence |
| `Corvus.Text.Json.Validator.Tests` | net9.0, net10.0, net481 | Dynamic schema validator (runtime compilation) |
| `Corvus.Text.Json.CodeGenerator.Tests` | net10.0 | CLI code generator |
| `Corvus.Text.Json.Migration.Analyzers.Tests` | net10.0 | V4→V5 migration Roslyn analyzers and code fixes |
| `Corvus.Numerics.Tests` | net9.0, net10.0, net481 | `BigNumber` / `BigInteger` arithmetic |

### Supporting projects (not runnable)

| Project | Purpose |
|---------|---------|
| `Corvus.Text.Json.Tests.GeneratedModels` | Source-generated V5 model types used by the main test suite |
| `Corvus.Text.Json.Tests.GeneratedModels.OptionalAsNullable` | V5 models with `OptionalAsNullable` enabled |
| `Corvus.Text.Json.Tests.MigrationModels.V4` | V4-generated model types (references `src-v4/Corvus.Json.ExtendedTypes`) |
| `Corvus.Text.Json.Tests.MigrationModels.V5` | V5 equivalents of the V4 models |
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

## Running a single test class or method

```powershell
# Single class
dotnet test Corvus.Text.Json.slnx --filter "ClassName=Corvus.Text.Json.Tests.ParsedJsonDocumentTests&category!=failing&category!=outerloop"

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

The `Corvus.Text.Json.Tests.MigrationModels.V5` project contains pre-generated V5 types from JSON schemas in `tests/Corvus.Text.Json.Tests.MigrationSchemas/`. These files are generated by the `generatejsonschematypes` CLI tool and committed to the repository.

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

### V5 test generator

The `Corvus.JsonSchemaTestSuite.CodeGenerator` project reads the submodule and writes xUnit test classes into `tests/Corvus.Text.Json.Tests/JsonSchemaTestSuite/`:

```powershell
dotnet run --project tests\Corvus.JsonSchemaTestSuite.CodeGenerator
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

### V4 specs generator

The `Corvus.JsonSchema.SpecGenerator` project (in `src-v4/`) generates SpecFlow `.feature` files into `src-v4/Corvus.Json.Specs/Features/JsonSchema/`:

```powershell
dotnet run --project src-v4\Corvus.JsonSchema.SpecGenerator -- `
    JSON-Schema-Test-Suite `
    src-v4\Corvus.Json.Specs\Features\JsonSchema\ `
    src-v4\Corvus.JsonSchema.SpecGenerator\JsonSchemaOrgTestSuiteSelector.jsonc
```

The three positional arguments are:

| Argument | Purpose |
|----------|---------|
| `inputDir` | Path to the `JSON-Schema-Test-Suite/` submodule |
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

There is also an `OpenApiTestSuiteSelector.jsonc` for the OpenAPI test suite specs.

### After regenerating

After running both generators, rebuild and re-run the tests to verify:

```powershell
dotnet build Corvus.Text.Json.slnx
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"
```
