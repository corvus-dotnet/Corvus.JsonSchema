# Corvus.Text.Json — Copilot Instructions

## Project Overview

This is **Corvus.Text.Json**, a high-performance JSON library for .NET that extends `System.Text.Json` with pooled-memory parsing, JSON Schema validation (draft 2019-09 and 2020-12), mutable document building, extended numeric types (`BigNumber`, `BigInteger`), and NodaTime integration. It includes a Roslyn incremental source generator and a CLI code generator (`corvusjson`) that produce strongly-typed C# from JSON Schema files.

The repo structure mirrors the dotnet/runtime repository conventions: shared source files in `Common/`, polyfills from `System.Private.CoreLib/`, and explicit `<Compile>` item groups (no glob includes).

## Skills Inventory

20 skills in `.github/skills/` provide deep context on specific areas. Copilot loads them on demand.

| Skill | Area |
|-------|------|
| `corvus-build-and-test` | Building, testing, TFM targeting, solution files |
| `corvus-codegen` | Source generator and CLI code generation from JSON Schema |
| `corvus-keywords-and-validation` | JSON Schema keywords, vocabularies, validation handlers |
| `corvus-standalone-evaluator` | Validation-only evaluator generation and annotation collection |
| `corvus-parsed-documents-and-memory` | Parsing, IJsonElement, memory model, UTF-8 transcoding |
| `corvus-mutable-documents` | JsonWorkspace, JsonDocumentBuilder, mutation, JSON Patch |
| `corvus-buffer-and-pooling` | stackalloc/ArrayPool/ThreadStatic pooling patterns |
| `corvus-low-alloc-data-structures` | Ref-struct collections, SIMD, hash sets |
| `corvus-numeric-types` | BigNumber, numeric parsing, format selection |
| `corvus-ecma-regex` | ECMAScript → .NET regex translation |
| `corvus-query-languages` | JSONata, JMESPath, JsonLogic, JSONPath |
| `corvus-yaml` | YAML ↔ JSON conversion |
| `corvus-analyzers` | Roslyn analyzers (CTJ001-CTJ010) |
| `corvus-benchmarks` | BenchmarkDotNet execution, B/C baseline convention |
| `corvus-docs-website` | Documentation site build pipeline and playgrounds |
| `corvus-bowtie-testing` | Bowtie conformance testing against JSON Schema Test Suite |
| `corvus-test-suite-regeneration` | Regenerating test classes from the submodule |
| `corvus-v4-migration` | V4 → V5 migration patterns and analyzers |
| `ref-struct-delegates` | Custom delegates for ref-struct parameters |
| `reviewing-skills` | Post-work review and maintenance of skills and instructions |

## Build & Test

```bash
# Build the full solution
dotnet build Corvus.Text.Json.slnx

# Run the standard test suite — exclude the 'failing' and 'outerloop' categories
dotnet test Corvus.Text.Json.Test.slnx --filter "category!=failing&category!=outerloop"

# Run a single test class
dotnet test Corvus.Text.Json.Test.slnx --filter "FullyQualifiedName~ParsedJsonDocumentTests&category!=failing&category!=outerloop"

# Run a single test method
dotnet test Corvus.Text.Json.Test.slnx --filter "FullyQualifiedName~ParseValidUtf8BOM&category!=failing&category!=outerloop"
```

Always exclude `failing` and `outerloop` categories when running tests. Never run all tests without these filters.

Always use `FullyQualifiedName~` (substring match) for test filters — not `ClassName=`. The `ClassName` filter does not work reliably in this repo.

**Solution files:**

| Solution | Purpose |
|----------|---------|
| `Corvus.Text.Json.slnx` | Main V5 solution — build only |
| `Corvus.Text.Json.Test.slnx` | Tests — use for `dotnet test` |
| `Corvus.Text.Json.Benchmarks.slnx` | Benchmark projects only |

`TreatWarningsAsErrors=true` is set across all projects — the build will fail on any warning.

See the `corvus-build-and-test` skill for TFM targeting, test project mapping, and common build failure diagnosis.

## Architecture

### Core abstractions

- **`IJsonDocument`** — base interface for pooled-memory JSON documents; always `Dispose()` to return memory to `ArrayPool<byte>`.
- **`ParsedJsonDocument<T>`** — read-only, immutable parsed document.
- **`JsonDocumentBuilder<T>`** — mutable variant; tracks a `ulong _version` so stale element references throw `InvalidOperationException`.
- **`IJsonElement<T> where T : struct, IJsonElement<T>`** — CRTP-style generic interface that lets consumers define custom element types while sharing the same traversal and schema API. Every custom JSON type implements this.

### Partial-class organisation

The main `JsonElement` type is split across many files by concern, e.g.:
- `JsonElement.cs` — core struct
- `JsonElement.Parse.cs` — parsing
- `JsonElement.JsonSchema.cs` — schema validation
- `JsonElement.Mutable.cs` — mutable operations
- `JsonElementHelpers.*.cs` — DateTime, Uri, numeric, NodaTime helpers (10+ files)

Follow this pattern when adding functionality: keep the core struct untouched and add a new partial file named `JsonElement.<Concern>.cs`.

### Code generation

Two code-gen mechanisms are used together:
1. **Roslyn `IIncrementalGenerator`** (`src/Corvus.Text.Json.SourceGenerator/`) — triggered at build time via `JsonSchemaTypeGeneratorAttribute`. `EmitCompilerGeneratedFiles=true` writes output to `obj/` for inspection.
2. **CLI tool** (`src/Corvus.Json.Cli.Core/`) — `corvusjson jsonschema` (package: `Corvus.Json.Cli`) generates C# from JSON Schema for use outside the build pipeline (e.g., the `tests/Corvus.Text.Json.Tests.GeneratedModels/` project). The legacy `generatejsonschematypes` command (package: `Corvus.Json.CodeGenerator`) still works as a shim but defaults to the V4 engine.

**IMPORTANT:** When writing documentation, examples, or instructions that reference Source Generator attributes or CLI tool options, always verify the exact parameter names and types by checking the source code:
- **Source Generator attribute:** `src/Corvus.Text.Json.SourceGenerator/IncrementalSourceGenerator.cs` — the `JsonSchemaTypeGeneratorAttribute` is emitted by the generator. Constructor: `(string location, bool rebaseToRootPath = false)`; settable property: `EmitEvaluator` (bool). Applies to `partial struct` only (`AttributeTargets.Struct`).
- **CLI tool options:** `src/Corvus.Json.Cli.Core/GenerateCommand.cs` — defines all command-line settings including `--assertFormat` (bool, default true), `--rootNamespace`, `--outputPath`, `--outputRootTypeName`, `--engine`, `--codeGenerationMode`, etc. The CLI command is `corvusjson jsonschema` (package: `Corvus.Json.Cli`); the legacy `Corvus.Json.CodeGenerator` package (command: `generatejsonschematypes`) still works but defaults to the V4 engine.

Do **not** invent or hallucinate option names. If unsure, read the source files above before writing.

### netstandard2.0 compatibility

The main library targets `net9.0;net10.0;netstandard2.0;netstandard2.1`. On `netstandard2.0`, polyfill source files are linked directly from `System.Private.CoreLib/src/` (nullable attributes, `CallerArgumentExpressionAttribute`, `Index`/`Range`, etc.). Conditional `<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'>` blocks in the `.csproj` control this. Do not add package polyfills for things already covered by these linked files.

## Converting UTF-8 bytes to strings in tests

When a method under test writes its output to a `Span<byte>` (i.e. a UTF-8 Utf8 format method), use `JsonReaderHelper.TranscodeHelper` to turn the result into a `string` for assertion. This is the standard cross-platform approach used throughout the test suite — it abstracts over the `#if NET` / `netstandard2.0` boundary so tests don't need `#if` blocks of their own.

```csharp
Span<byte> destination = stackalloc byte[100];

bool success = JsonElementHelpers.TryFormatCurrency(
    isNegative, integral, fractional, exponent,
    destination, out int bytesWritten, precision, formatInfo);

Assert.True(success);
string result = JsonReaderHelper.TranscodeHelper(destination.Slice(0, bytesWritten));
Assert.Equal(expected, result);
```

**Available overloads** (all in `Corvus.Text.Json.Reader.JsonReaderHelper`, internal):

| Signature | Use when |
|---|---|
| `string TranscodeHelper(ReadOnlySpan<byte>)` | You need a `string` from a UTF-8 buffer — most common in tests |
| `int TranscodeHelper(ReadOnlySpan<byte>, Span<char>)` | You already have a `char` destination buffer |
| `bool TryTranscode(ReadOnlySpan<byte>, Span<char>, out int)` | Non-throwing variant; returns `false` if the destination is too small |
| `int TranscodeHelper(ReadOnlySpan<char>, Span<byte>)` | Reverse direction: `char` → UTF-8 bytes |

On `net9.0`+ these delegate to `Encoding.UTF8.GetString(ReadOnlySpan<byte>)` and related span APIs. On `netstandard2.0` / `net481` they fall back to `unsafe fixed`-pointer overloads. Invalid UTF-8 always throws `InvalidOperationException` (wrapping `DecoderFallbackException`) rather than letting the raw codec exception escape.

## stackalloc / ArrayPool rent pattern

Throughout the codebase, temporary byte or char buffers follow a single consistent pattern: stack-allocate for small sizes, rent from `ArrayPool` for larger ones. Always use `try/finally` to guarantee the rented array is returned. See the `corvus-buffer-and-pooling` skill for full rules, tier hierarchy, and thread-static cache patterns.

```csharp
byte[]? rentedArray = null;

Span<byte> buffer = length <= JsonConstants.StackallocByteThreshold
    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
    : (rentedArray = ArrayPool<byte>.Shared.Rent(length));

try
{
    // use buffer.Slice(0, length) — the rented array may be larger than requested
    DoWork(buffer.Slice(0, length));
}
finally
{
    if (rentedArray != null)
    {
        ArrayPool<byte>.Shared.Return(rentedArray);
    }
}
```

**Thresholds** (defined in `JsonConstants`, shared via `src/Common/`):

| Constant | Value | Use for |
|---|---|---|
| `JsonConstants.StackallocByteThreshold` | 256 | `byte` / UTF-8 buffers |
| `JsonConstants.StackallocCharThreshold` | 128 | `char` buffers (256 / 2) |


## Key Conventions

- **Tone** — avoid aggressive language (e.g. "crush", "destroy", "kill", "dominate") when describing benchmark results or performance comparisons. Use neutral terms like "faster", "ahead of", "leads", "wins".
- **`EnableDefaultCompileItems=false` in select projects** — the following projects disable auto-discovery and require explicit `<Compile Include="..." />` entries for every `.cs` file: `Corvus.Text.Json`, `Corvus.Text.Json.Tests`, `Corvus.Text.Json.CodeGeneration`, `Corvus.Text.Json.SourceGenerator`, `Corvus.Text.Json.Compatibility`, and the four source-generator analyzer projects (`*.Jsonata.SourceGenerator`, `*.JMESPath.SourceGenerator`, `*.JsonLogic.SourceGenerator`, `*.JsonPath.SourceGenerator`). All other projects (JsonPath, JMESPath, Jsonata, JsonLogic, Patch, Yaml, Validator, and most test projects) auto-discover files — no `<Compile>` entry needed.
- **`LangVersion=preview`** — preview C# language features are intentionally used. Prefer raw string literals (`"""`) for JSON and multi-line strings to avoid escape sequences. Use UTF-8 string literals (`"..."u8`) where a `ReadOnlySpan<byte>` is needed.
- **`AllowUnsafeBlocks=true`** — unsafe pointer arithmetic is used in numeric and UTF-8 hot paths; this is expected.
- **Nullable annotations** — enabled in all library projects (`Nullable=enable`), disabled in test projects. Public APIs must have complete XML doc comments; `CS1591` is treated as an error in test projects.
- **Shared source via `Common/`** — files in `Common/src/` and `Common/tests/` are shared across projects via `<Compile Include="$(CommonPath)..." Link="..." />`. Do not duplicate these; link them instead.
- **`SR` alias** — `using SR = Resources.Strings;` (or the project-specific variant) is a global using. Use `SR.ExceptionMessageName` for all user-facing strings; define new strings in the `.resx` file.
- **Disabled warnings** — `JSON001`, `xUnit1031`, `xUnit2013`, `CS8500`, `IDE0065`, `IDE0290` are suppressed project-wide; don't add `#pragma warning disable` for these.
- **EditorConfig** — 4-space indentation, `csharp_new_line_before_open_brace = all`. Generated files must be marked `generated_code = true` in `.editorconfig` entries.
- **JSON Schema test suite** — `JSON-Schema-Test-Suite/` is a git submodule. Run `.\update-json-schema-test-suite.ps1` to update the submodule and regenerate all V5 test classes and V4 spec feature files. The script handles cleaning old files and running both V4 selectors (JsonSchema + OpenApi30). See `docs/RunningTests.md` for manual regeneration details.
- **`BigNumber`** — the custom arbitrary-precision decimal struct lives in `Corvus.Numerics`. Prefer it over `decimal` when the JSON value may have precision beyond 28 significant digits.
- **Test-first bug fixes** — never implement a fix for a suspected bug without first writing a test that reproduces the problem. The test must fail before the fix and pass after. If you cannot reproduce the bug with a test, do not change production code.
- **Data-driven coverage improvement** — when working to improve code coverage, ONLY write tests that target specific uncovered branches/lines identified in Cobertura XML coverage reports. Never write generic tests for already-covered functions hoping they might help. The process is: (1) collect coverage with `--collect:"XPlat Code Coverage"`, (2) merge reports with `reportgenerator`, (3) parse the Cobertura XML to find exact uncovered line ranges, (4) read the actual source code at those lines to understand the uncovered logic, (5) devise expressions/inputs that exercise those specific code paths, (6) verify with the reference implementation where applicable. The coverage report is the sole source of truth for what needs testing — not guesswork about what "might" be uncovered.
- **Doc samples: prefer `Parse` over `ParseValue`** — documentation examples should show `ParsedJsonDocument<T>.Parse(...)` with `using` to promote pooled-memory best practice. `ParseValue` creates non-disposable copies. Use `ParseValue` only where `Parse` is impractical (e.g., inline dictionary initializers for small constants).
- **Doc samples: use implicit `JsonElement.Source` conversions** — write `PatchBuilder.Add("/name"u8, "Alice")`, `.Replace("/version"u8, 2)` instead of wrapping scalars in `ParseValue`.
- **Doc samples: only import `Corvus.Text.Json`** — doc blocks should not import `System.Text.Json`. Use fully-qualified names for `System.Text.Json` types when needed.

## Documentation Code Sample Verification

**CRITICAL:** All C# code samples in documentation must compile against the current codebase.

The file `docs/code-sample-catalog.yaml` is the authoritative inventory of every fenced code block across all documentation, skills, and instruction files. It records file paths, block line ranges, languages, categories, and verification status. See `docs/CodeSampleCatalog.md` for the full user guide.

### Step 0 — build existing example projects first

Before verifying any markdown code blocks, build the existing compilable projects. This confirms the real code compiles and gives you a reference for cross-checking README samples.

```powershell
dotnet build docs\ExampleRecipes\ExampleRecipes.slnx
```

If any ExampleRecipes project fails to build, fix it before moving on — the README code blocks are derived from these projects.

### Everyday workflow — modified files only

You do **not** need to process the entire catalog. When you edit a documentation file:

1. **Build** any affected ExampleRecipes projects first (if the file is under `docs/ExampleRecipes/`).
2. **Verify** the compilable C# samples in that file still compile (cross-reference against companion `.cs` files for ExampleRecipes READMEs, or use a C# script file for standalone docs).
3. **Update the catalog** for just that file:
   ```powershell
   .\docs\update-code-sample-catalog.ps1 -UpdateFile <relative-path>
   ```
4. Set `verified: true` for each block you confirmed compiles.
5. Run `-Check` before committing to confirm the catalog is in sync:
   ```powershell
   .\docs\update-code-sample-catalog.ps1 -Check
   ```

This keeps the catalog accurate incrementally — a full re-scan is only needed after bulk changes or submodule updates.

### When you detect user file changes

If you observe that the user has modified a documentation file (e.g., through file-change notifications or when resuming work), proactively run `-UpdateFile` to refresh the catalog entries for that file.

### Triage rules for code blocks

| Block type | Action |
|---|---|
| Complete C# (has `using` + statements/types) | Must compile — category `compilable` |
| Method body / statement snippets | Wrap in a minimal harness and compile — category `compilable` |
| Fragments (single expressions, partial lines) | Skip — category `fragment` |
| V4 "before" examples (migration docs) | Skip — category `v4-before` |
| All blocks under `docs/V4/` | Skip — category `v4-before` (entire file is V4) |
| `docs/MigrationAnalyzers.md` blocks | Skip — category `fragment` (paired before/after examples) |
| Intentionally bad patterns (analyzer docs) | Skip — category `bad-pattern` |
| Non-C# (JSON, YAML, bash, XML) | Skip — no category field in catalog |

### Automated triage

The script `docs/triage-code-samples.ps1` performs **heuristic** categorization by analyzing block content (V4 namespace markers, fragment detection, ExampleRecipes cross-referencing). It does **not** verify compilation — blocks it marks `verified: true` are only cross-referenced against companion `.cs` files, not compiled. Always compile-verify separately.

```powershell
.\docs\triage-code-samples.ps1 -DryRun                  # Preview changes
.\docs\triage-code-samples.ps1                           # Apply heuristic categories
.\docs\triage-code-samples.ps1 -Section example-recipes  # Single section
.\docs\triage-code-samples.ps1 -File docs\JsonPath.md    # Single file
```

For first-time verification: (1) run the full catalog update, (2) run the triage script, (3) compile-verify the remaining `compilable` blocks, (4) run `-Check`.

**Prioritization for first-time passes:** With hundreds of compilable blocks, verify in this order: (1) files with known recent changes, (2) quick-start and getting-started sections, (3) standalone docs (`docs/*.md`) — these are the highest-risk for compilation bugs, (4) skills files. ExampleRecipes blocks that cross-reference successfully against companion `.cs` files can be trusted — focus on blocks not in `.cs` files.

### ExampleRecipes verification

README code samples that match companion `.cs` files are verified by the triage script's cross-referencing. However, many READMEs also contain **supplementary educational blocks** (patterns, alternatives, explanations) that are not in any `.cs` file. These still need file-based app verification. The `.cs` file is always the source of truth — update the README to match, not the other way around.

### File-based app verification tips

When combining multiple doc blocks into one verification file:
- Gather **all `using` directives** at the top — `using` after top-level statements causes CS1529.
- Wrap each block in its own scope `{ ... }` to isolate variable names.
- Only one verification file should be open at a time (each is a separate compilation unit).

### Known compilation traps

When verifying samples, watch for these patterns that look correct but fail to compile:

- **`ParsedJsonDocument<T>.Parse("""..."""u8)`** — `Parse` takes `ReadOnlyMemory<byte>`, not `ReadOnlySpan<byte>` (which the `u8` suffix produces). Remove the `u8` suffix.
- **`ArrayBuilder.AddProperty()`** — does not exist. Array elements use `AddItem()`. `AddProperty(name, value)` is only on `ObjectBuilder`.
- **`using System.Text.Json;` alongside `using Corvus.Text.Json;`** — causes ambiguity for `JsonElement`, `Utf8JsonWriter`, and `JsonWriterOptions` which exist in both namespaces.

### Catalog maintenance script

```powershell
.\docs\update-code-sample-catalog.ps1                           # Full update (preserve on-disk annotations)
.\docs\update-code-sample-catalog.ps1 -UpdateFile docs\X.md    # Re-scan one file
.\docs\update-code-sample-catalog.ps1 -Check                    # Verify catalog matches files
.\docs\update-code-sample-catalog.ps1 -Generate                 # Fresh catalog (RESETS all annotations)
.\docs\update-code-sample-catalog.ps1 -Stats                    # Print summary statistics
```

The default full update preserves `category` and `verified` annotations from the catalog file on disk. The `-Generate` flag resets all annotations to defaults. See `docs/CodeSampleCatalog.md` for the full guide including file-based app verification patterns.

## JsonWorkspace and Mutable Documents

`JsonWorkspace` is a scoped container for pooled memory used during mutable JSON operations. See the `corvus-mutable-documents` skill for the full API including multi-builder patterns, empty builders, cloning, and JSON Patch.

**Canonical parse-build-mutate-serialize pattern:**

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);

using JsonDocumentBuilder<JsonElement.Mutable> builder =
    sourceDoc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;
// ... manipulate root ...
string result = root.ToString();
```

**Lifetime:** always use a `using` block. Prefer `JsonWorkspace.Create()` (rents from thread-local cache). Use `CreateUnrented()` only when you need explicit lifetime control outside a `using` block.

### Test helper types

- **`DummyDocument`** — minimal `IJsonDocument` mock that returns a fixed `JsonTokenType`. Used when tests need an `IJsonDocument` reference but don't require real document operations.
- **`DummyResultsCollector`** — mock `IJsonSchemaResultsCollector` that counts context-nesting and schema-location calls; used in schema validation unit tests.


## Documentation Website

The documentation site lives in `docs/website/`. See `docs/website/DEVELOPMENT.md` for the full guide.

### Key architecture

- **Source** → `docs/website/site/` contains content markdown, taxonomy YAML, theme (Razor views, SCSS, JS), and tools.
- **Build** → `docs/website/build.ps1` runs a 12-step pipeline (steps 0-11) that compiles everything into `docs/website/.output/`.
- **Serving** → The local dev server (`build.ps1 -ServeOnly` or `preview.ps1`) serves static files from `.output/`, **not** from the source theme directory. Editing source files (views, SCSS, JS) has no effect until the relevant build step is re-run.
- **IMPORTANT: Stop the server before rebuilding.** The build script deletes and recreates `.output/`. On Windows, the Node file server holds file locks that prevent deletion, causing the build to hang indefinitely. Always stop the serving process (`Stop-Process -Id <PID>`) before running `build.ps1`.

### Generated vs hand-authored files

Many files under `site/` are **auto-generated** by `build.ps1` and are `.gitignored`:
- `site/theme/corvus/views/api/v5/index.cshtml` and `v4/index.cshtml` — generated by `XmlDocToMarkdown`
- `site/theme/corvus/views/Shared/_ApiSidebarV5.cshtml` and `V4` — generated
- `site/content/Api-v5/`, `site/content/Api-v4/` — generated namespace markdown (except `namespaces/` and `examples/` subdirs)
- `site/taxonomy/api-v5/`, `site/taxonomy/api-v4/` — generated taxonomy YAML
- `site/content/Docs/`, `site/content/Examples/` — generated from doc-descriptors and ExampleRecipes
- `site/taxonomy/docs/`, `site/taxonomy/examples/` — generated taxonomy YAML

**Hand-authored source files** live in `site/source/` and are copied into the target tree by build step 0:
- `source/content/Docs/Overview.md`, `source/content/Examples/Overview.md`
- `source/taxonomy/{docs,examples,api,api-v5,api-v4}/index.yml`

Other hand-authored files (committed directly, not generated): `site/content/GettingStarted/`, `site/content/Home/`, `site/content/Api-v5/namespaces/`, `site/content/Api-v5/examples/`, and the theme's SCSS/JS/layout views.

### Incremental rebuilds

After changing source files, you must rebuild the affected pipeline steps **and** the `.output/` copy. Common patterns:

| Changed | Re-run |
|---|---|
| SCSS styles | `npx sass theme\corvus\assets\css\scss\main.scss .output\main.css --style=compressed --no-source-map` |
| JavaScript | `Copy-Item theme\corvus\assets\js\*.js .output\` |
| API page templates / XmlDocToMarkdown tool | Rebuild tool + regenerate HTML (see DEVELOPMENT.md) |
| Content markdown (non-API) | Steps 3–6 of `build.ps1` |
| Library source code | `dotnet build` the library, then regenerate API docs |

### XmlDocToMarkdown tool

The generator at `docs/website/tools/XmlDocToMarkdown/` processes XML docs + assemblies into markdown, taxonomy, Razor views, and per-type HTML pages. It supports multi-assembly input (V4 has 8 libraries), versioned output with engine switcher, and per-version search indices. Key entry points: `ApiViewGenerator.cs` (Razor view generation), `HtmlPageGenerator.cs` (per-type HTML), `MarkdownGenerator.cs` (namespace markdown).

## Playgrounds

Six Blazor WASM playgrounds provide interactive browser-based tools for trying out the libraries. They live under `docs/` and share the same architecture: a Blazor WASM app with Monaco editor integration bundled via esbuild.

| Playground | Directory | Port |
|-----------|-----------|------|
| JSON Schema | `docs/playground/` | 5281 |
| JSONata | `docs/playground-jsonata/` | 5280 |
| JMESPath | `docs/playground-jmespath/` | — |
| JsonLogic | `docs/playground-jsonlogic/` | — |
| JSONPath | `docs/playground-jsonpath/` | — |
| YAML | `docs/playground-yaml/` | — |

### Running a playground

```powershell
# 1. Build the JavaScript bundle (only needed after changing JS/Monaco assets)
cd docs/playground-jsonata
npm ci
npm run bundle

# 2. Start the Blazor WASM dev server on a fixed port
$env:ASPNETCORE_URLS = "http://127.0.0.1:5280"
dotnet run --project docs/playground-jsonata/src/Corvus.Text.Json.Jsonata.Playground/Corvus.Text.Json.Jsonata.Playground.csproj
```

Use `ASPNETCORE_URLS` to pin the port — the `--urls` flag does not work with the WASM app host.

**IMPORTANT:** Stop the server before rebuilding. The WASM host holds file locks that prevent rebuild from completing.

**Error messages in WASM:** `SR.Format` does not work correctly in Blazor WASM because `System.Resources.UseSystemResourceKeys` returns `true`, causing it to fall back to `string.Join` instead of `string.Format`. Five of the six playgrounds (all except JSON Schema) have an `EvaluationService.FixBrokenSRFormat()` method that compensates by detecting unsubstituted `{0}` placeholders and re-applying `string.Format`. All exception messages displayed to the user in those playgrounds must go through this method.

## Benchmarks

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

After making code generator changes, regenerate all C/ directories:

```bash
# Clean the C/ directory first (old files cause compilation errors)
Remove-Item -Recurse -Force benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C\*

# Regenerate with CLI tool
dotnet run --project src\Corvus.Json.CodeGenerator -f net10.0 -c Release -- <schema-path> --rootNamespace Corvus.<Name>Benchmark.Current --outputRootTypeName <Name>Schema --outputPath benchmarks\Corvus.Text.Json.<Name>BenchmarkModels\C --engine V5
```

All 37+ benchmark models follow the same pattern — no special cases. (GeoJson previously required special handling for long file paths, but this was fixed by the path truncation collision fix in `GenerationDriverV5.cs`.)

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
dotnet test <relevant-test-project> -f net10.0 --filter "category!=failing&category!=outerloop" -v q --no-restore

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

## Namespaces

| Namespace | Purpose |
|---|---|
| `Corvus.Text.Json` | Public API — core types, parsing, schema validation |
| `Corvus.Text.Json.Internal` | Internal helpers, enumerators, metadata |
| `Corvus.Text.Json.Patch` | RFC 6902 JSON Patch and JSON Merge Patch |
| `Corvus.Text.Json.Canonicalization` | RFC 8785 JSON Canonicalization Scheme (JCS) |
| `Corvus.Text.Json.Jsonata` | JSONata expression evaluator |
| `Corvus.Text.Json.JMESPath` | JMESPath query evaluator |
| `Corvus.Text.Json.JsonLogic` | JsonLogic rule engine |
| `Corvus.Text.Json.JsonPath` | JSONPath (RFC 9535) query evaluator |
| `Corvus.Text.Json.Yaml` | YAML 1.2 ↔ JSON conversion (full integration) |
| `Corvus.Text.Json.Validator` | Runtime dynamic schema validation via Roslyn compilation |
| `Corvus.Text.Json.Compatibility` | Interoperability bridge between V5 types, V4 `Corvus.Json.ExtendedTypes`, and `System.Text.Json` |
| `Corvus.Numerics` | `BigNumber`, `BigInteger` support |
| `Corvus.NodaTimeExtensions` | NodaTime (`LocalDate`, `OffsetDateTime`, `Period`) helpers |

## Additional Packages

- **`Corvus.Text.Json.Patch`** — RFC 6902 JSON Patch, JSON Merge Patch, and JSON Diff. See `docs/JsonPatch.md`.
- **`Corvus.Text.Json.Canonicalization`** — RFC 8785 JCS lives in the core `Corvus.Text.Json` package (not a separate package). See `docs/JsonCanonicalization.md`.
- **`Corvus.Text.Json.Validator`** — Runtime dynamic schema validation: loads schemas at runtime, compiles validators via Roslyn, caches results. Supports Draft 4–2020-12 and OpenAPI 3.0. See `docs/Validator.md`.
- **`Corvus.Text.Json.Compatibility`** — Interoperability layer that references both V5 (`Corvus.Text.Json`) and V4 (`Corvus.Json.ExtendedTypes`) plus `System.Text.Json`, providing bridge helpers for migration scenarios. Uses `EnableDefaultCompileItems=false`.

## All Benchmark Projects

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
