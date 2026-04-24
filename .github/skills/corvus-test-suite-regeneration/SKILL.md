---
name: corvus-test-suite-regeneration
description: >
  Regenerate JSON Schema test classes from the JSON-Schema-Test-Suite git submodule.
  Covers the update-json-schema-test-suite.ps1 master script, the V5 test generator
  (Corvus.JsonSchemaTestSuite.CodeGenerator), the V4 spec generator with its two
  required selector runs, exclusion configuration, and output directory structure.
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
3. Regenerates V4 spec feature files (2 selector runs)

## V5 Test Generator

The `Corvus.JsonSchemaTestSuite.CodeGenerator` project generates test classes for 3 suites,
all of which are subdirectories under `tests/Corvus.Text.Json.Tests/`:

| Output subdirectory | Purpose |
|---------------------|---------|
| `tests/Corvus.Text.Json.Tests/JsonSchemaTestSuite/` | Full type-based validation tests |
| `tests/Corvus.Text.Json.Tests/StandaloneEvaluatorTestSuite/` | Evaluator-only tests |
| `tests/Corvus.Text.Json.Tests/AnnotationTestSuite/` | Annotation collection tests |

These are NOT separate projects — they are subdirectories within `Corvus.Text.Json.Tests`.

**CRITICAL:** The V5 generator must be run **from its `bin/` directory**, not via `dotnet run`, because `appsettings.json` uses relative paths that resolve from the exe's working directory.

```powershell
# Build the generator
dotnet build tests\Corvus.JsonSchemaTestSuite.CodeGenerator -c Release

# Run from bin directory
Push-Location tests\Corvus.JsonSchemaTestSuite.CodeGenerator\bin\Release\net10.0
.\Corvus.JsonSchemaTestSuite.CodeGenerator.exe > $null
Pop-Location
```

Redirect stdout to `$null` to avoid PowerShell buffer overflow.

## V4 Spec Generator

V4 requires **TWO separate runs** — missing either silently drops features:

### Run 1: JSON Schema Test Suite
```powershell
# Uses JsonSchemaOrgTestSuiteSelector.jsonc
# Input: JSON-Schema-Test-Suite/
```

### Run 2: OpenAPI Test Suite
```powershell
# Uses OpenApiTestSuiteSelector.jsonc
# Input: OpenApi-Test-Suite/
```

Both selector files are in `src-v4/Corvus.JsonSchema.SpecGenerator/`.

## Critical Rules

1. **Always delete old generated files before regenerating.** The generators only create files — they never delete files that are no longer selected by exclusion rules. Stale files cause compilation errors or false test passes.

2. **Run from the correct working directory.** The V5 generator uses relative paths from `appsettings.json`.

3. **Don't forget either V4 run.** Missing the OpenAPI selector run silently drops OpenAPI-specific test features.

## Exclusion Configuration

Test exclusions (for known spec failures or draft-specific skips) are configured in:
- V5: `appsettings.json` in the generator project
- V4: The `.jsonc` selector files

## Cross-References
- For running the generated tests, see `corvus-build-and-test`
- For the JSON Schema keyword system, see `corvus-keywords-and-validation`
- Full guide: `docs/RunningTests.md`
