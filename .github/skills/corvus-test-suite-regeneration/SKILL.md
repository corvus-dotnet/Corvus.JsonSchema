---
name: corvus-test-suite-regeneration
description: >
  Regenerate JSON Schema test classes from the JSON-Schema-Test-Suite git submodule.
  Covers the update-json-schema-test-suite.ps1 master script, the V5 test generator
  (Corvus.JsonSchemaTestSuite.CodeGenerator), exclusion configuration, and output
  directory structure.
  USE FOR: updating the test suite submodule, regenerating test classes after submodule
  or generator changes, understanding the test infrastructure.
  DO NOT USE FOR: running existing tests (use corvus-build-and-test), writing manual
  tests.
---

# Test Suite Regeneration

## Master Script

```powershell
.\update-json-schema-test-suite.ps1
```

This handles the full workflow:
1. Updates the `JSON-Schema-Test-Suite` git submodule
2. Regenerates V5 test classes (3 output directories)

## V5 Test Generator

The `Corvus.JsonSchemaTestSuite.CodeGenerator` project generates test classes for 3 suites,
output into two test projects:

| Output subdirectory | Purpose |
|---------------------|---------|
| `tests/Corvus.Text.Json.SchemaTestSuite.Tests/JsonSchemaTestSuite/` | Full type-based validation tests |
| `tests/Corvus.Text.Json.EvaluatorTestSuite.Tests/StandaloneEvaluatorTestSuite/` | Evaluator-only tests |
| `tests/Corvus.Text.Json.EvaluatorTestSuite.Tests/AnnotationTestSuite/` | Annotation collection tests |

The output paths are configured in the generator's `appsettings.json` (`outputPath`,
`evaluatorOutputPath`, `annotationOutputPath`). The emitted fixtures are OS-independent
(embedded paths use `/`, embedded JSON uses LF), so regeneration is byte-stable across
operating systems.

**CRITICAL:** The V5 generator must be run **from its `bin/` directory**, not via `dotnet run`, because `appsettings.json` uses relative paths that resolve from the working directory.

```powershell
# Build the generator
dotnet build tests\Corvus.JsonSchemaTestSuite.CodeGenerator -c Release

# Run from bin directory (use `dotnet <dll>` so it works on every OS;
# the apphost is `.exe` on Windows only)
Push-Location tests\Corvus.JsonSchemaTestSuite.CodeGenerator\bin\Release\net10.0
dotnet .\Corvus.JsonSchemaTestSuite.CodeGenerator.dll > $null
Pop-Location
```

Redirect stdout to `$null` to avoid PowerShell buffer overflow.

## V4 Tests

V4 schema validation tests are in `tests-v4/Corvus.Json.Specs.Tests/` as MSTest tests.
These test V4 types (`Corvus.Json.ExtendedTypes`, `Corvus.Json.CodeGeneration.CSharp`)
using runtime Roslyn compilation via `JsonSchemaBuilderDriver`. The test classes were
generated from the same JSON-Schema-Test-Suite data and cover all drafts (4, 6, 7,
2019-09, 2020-12) plus OpenAPI 3.0.

The master script regenerates them too, with the V4 generator
(`tests-v4/Corvus.JsonSchema.TestSuite.CodeGenerator`), which deletes its own output directories and whose
`appsettings.json` paths are relative to the repository root.

## The suite's layout

The suite keeps every dialect's cases in one tree, `validation/tests` (the optional ones in `optional/`, and
`proposals/`, which is not generated). Each test case says which releases it applies to (`compatibility`), whether it
expects `format` to be asserted (`tags`: `format-assertion` or `format-annotation`), and the schemas it references
(`externalSchemas`), which are served to that test case alone, since two test cases may give one URI different
bodies.

## Critical Rules

1. **Always delete old generated files before regenerating.** The generators only create files — they never delete files that are no longer selected by exclusion rules. Stale files cause compilation errors or false test passes.

2. **Run from the correct working directory.** The V5 generator uses relative paths from `appsettings.json`.

## Exclusion Configuration

Test exclusions (for known spec failures or draft-specific skips) are configured in
`appsettings.json` in the V5 generator project, and in the V4 generator's.

## Cross-References
- For running the generated tests, see `corvus-build-and-test`
- For the JSON Schema keyword system, see `corvus-keywords-and-validation`
- Full guide: `docs/RunningTests.md`
