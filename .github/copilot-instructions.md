# Corvus.Text.Json — Copilot Instructions

## Project Overview

This is **Corvus.Text.Json**, a high-performance JSON library for .NET that extends `System.Text.Json` with pooled-memory parsing, JSON Schema validation (draft 2019-09 and 2020-12), mutable document building, extended numeric types (`BigNumber`, `BigInteger`), and NodaTime integration. It includes a Roslyn incremental source generator and a CLI code generator (`generatejsonschematypes`) that produce strongly-typed C# from JSON Schema files.

The repo structure mirrors the dotnet/runtime repository conventions: shared source files in `Common/`, polyfills from `System.Private.CoreLib/`, and explicit `<Compile>` item groups (no glob includes).

## Build & Test

```bash
# Build the full solution
dotnet build Corvus.Text.Json.slnx

# Run the standard test suite — exclude the 'failing' and 'outerloop' categories
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"

# Run a single test class
dotnet test Corvus.Text.Json.slnx --filter "ClassName=Corvus.Text.Json.Tests.ParsedJsonDocumentTests&category!=failing&category!=outerloop"

# Run a single test method
dotnet test Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParseValidUtf8BOM&category!=failing&category!=outerloop"
```

Always exclude `failing` and `outerloop` categories when running tests. Never run all tests without these filters.

`TreatWarningsAsErrors=true` is set across all projects — the build will fail on any warning.

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
2. **CLI tool** (`src/Corvus.Json.CodeGenerator/`) — `generatejsonschematypes` generates C# from JSON Schema for use outside the build pipeline (e.g., the `tests/Corvus.Text.Json.Tests.GeneratedModels/` project).

**IMPORTANT:** When writing documentation, examples, or instructions that reference Source Generator attributes or CLI tool options, always verify the exact parameter names and types by checking the source code:
- **Source Generator attribute:** `src/Corvus.Text.Json.SourceGenerator/IncrementalSourceGenerator.cs` — the `JsonSchemaTypeGeneratorAttribute` is emitted by the generator and defines: `Location` (string, required), `RebaseToRootPath` (bool), `EmitEvaluator` (bool).
- **CLI tool options:** `src/Corvus.Json.CodeGenerator/GenerateCommand.cs` — defines all command-line settings including `--assertFormat` (bool, default true), `--rootNamespace`, `--outputPath`, `--outputRootTypeName`, `--engine`, `--codeGenerationMode`, etc.

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

Throughout the codebase, temporary byte or char buffers follow a single consistent pattern: stack-allocate for small sizes, rent from `ArrayPool` for larger ones. Always use `try/finally` to guarantee the rented array is returned.

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

**Thresholds** (defined in `JsonConstants`, shared via `Common/src/`):

| Constant | Value | Use for |
|---|---|---|
| `JsonConstants.StackallocByteThreshold` | 256 | `byte` / UTF-8 buffers |
| `JsonConstants.StackallocCharThreshold` | 128 | `char` buffers (256 / 2) |

`BigNumber` uses a local `private const int StackAllocThreshold = 256` for the same purpose within that type.

Rules to follow:
- **Always** declare the nullable rented array before the ternary so it is in scope for the `finally` block.
- **Always** use the named constant, not a magic number, for the threshold and the `stackalloc` size.
- **Always** slice the span to `length` before use — `ArrayPool.Rent` returns an array that may be larger than requested.
- For fixed-size buffers where the maximum is always small (e.g., a 2 KB URI buffer in `Utf8Uri.ToString()`), a plain `stackalloc` without the pool fallback is acceptable.

## Key Conventions

- **No glob `<Compile>` items** — every source file must be explicitly listed in the `.csproj`. Adding a new `.cs` file requires a corresponding `<Compile Include="..." />` entry.
- **`LangVersion=preview`** — preview C# language features are intentionally used. Prefer raw string literals (`"""`) for JSON and multi-line strings to avoid escape sequences. Use UTF-8 string literals (`"..."u8`) where a `ReadOnlySpan<byte>` is needed.
- **`AllowUnsafeBlocks=true`** — unsafe pointer arithmetic is used in numeric and UTF-8 hot paths; this is expected.
- **Nullable annotations** — enabled in all library projects (`Nullable=enable`), disabled in test projects. Public APIs must have complete XML doc comments; `CS1591` is treated as an error in test projects.
- **Shared source via `Common/`** — files in `Common/src/` and `Common/tests/` are shared across projects via `<Compile Include="$(CommonPath)..." Link="..." />`. Do not duplicate these; link them instead.
- **`SR` alias** — `using SR = Resources.Strings;` (or the project-specific variant) is a global using. Use `SR.ExceptionMessageName` for all user-facing strings; define new strings in the `.resx` file.
- **Disabled warnings** — `JSON001`, `xUnit1031`, `xUnit2013`, `CS8500`, `IDE0065`, `IDE0290` are suppressed project-wide; don't add `#pragma warning disable` for these.
- **EditorConfig** — 4-space indentation, `csharp_new_line_before_open_brace = all`. Generated files must be marked `generated_code = true` in `.editorconfig` entries.
- **JSON Schema test suite** — `JSON-Schema-Test-Suite/` is a git submodule. The `Corvus.JsonSchemaTestSuite.CodeGenerator` project regenerates the xUnit test classes from it; re-run it after updating the submodule.
- **`BigNumber`** — the custom arbitrary-precision decimal struct lives in `Corvus.Numerics`. Prefer it over `decimal` when the JSON value may have precision beyond 28 significant digits.
- **Test-first bug fixes** — never implement a fix for a suspected bug without first writing a test that reproduces the problem. The test must fail before the fix and pass after. If you cannot reproduce the bug with a test, do not change production code.

## JsonWorkspace and Mutable Documents

### JsonWorkspace

`JsonWorkspace` is a scoped container for pooled memory used during mutable JSON operations. It tracks a set of `JsonDocumentBuilder<T>` instances via an `ArrayPool<IJsonDocument>`-backed array, and manages pooled `Utf8JsonWriter`/buffer rentals via `Utf8JsonWriterCache`.

**Creation:**

```csharp
// Preferred — rents from a thread-local cache (only one per thread is cached; nested calls allocate)
using JsonWorkspace workspace = JsonWorkspace.Create();

// When you need to control lifetime explicitly outside a using block
JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
```

**Lifetime:** always use a `using` block. `Dispose()` on a rented workspace returns it to the thread-local cache; on an unrented workspace it disposes all child documents and returns the backing array to `ArrayPool`.

### Creating mutable documents

The canonical pattern used throughout the codebase and tests:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);

// Convert an immutable element into a mutable builder document
using JsonDocumentBuilder<JsonElement.Mutable> builder =
    sourceDoc.RootElement.BuildDocument(workspace);

JsonElement.Mutable root = builder.RootElement;
// ... manipulate root ...
string result = root.ToString();
```

Multiple builders can share one workspace; each is registered in the workspace's document index:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse(json1);
using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse(json2);

using JsonDocumentBuilder<JsonElement.Mutable> builder1 = doc1.RootElement.BuildDocument(workspace);
using JsonDocumentBuilder<JsonElement.Mutable> builder2 = doc2.RootElement.BuildDocument(workspace);
```

You can also create an empty builder (no source document):

```csharp
using JsonDocumentBuilder<JsonElement.Mutable> builder =
    workspace.BuildDocument<JsonElement.Mutable>(initialCapacity: 30, initialValueBufferSize: 8192);
```

### Clones

Calling `.Clone()` on a mutable element produces an immutable `ParsedJsonDocument`-backed element that outlives the builder:

```csharp
using (JsonWorkspace workspace = JsonWorkspace.Create())
using (ParsedJsonDocument<JsonElement> parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[[]]]"))
using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.BuildDocument(workspace))
{
    clone = doc.RootElement[0].Clone(); // clone survives after the using blocks
}
Assert.Equal("[[]]", clone.GetRawText()); // still valid
```

### Test helper types

- **`DummyDocument`** — minimal `IJsonDocument` mock that returns a fixed `JsonTokenType`. Used when tests need an `IJsonDocument` reference but don't require real document operations.
- **`DummyResultsCollector`** — mock `IJsonSchemaResultsCollector` that counts context-nesting and schema-location calls; used in schema validation unit tests.

## Documentation Website

The documentation site lives in `docs/website/`. See `docs/website/DEVELOPMENT.md` for the full guide.

### Key architecture

- **Source** → `docs/website/site/` contains content markdown, taxonomy YAML, theme (Razor views, SCSS, JS), and tools.
- **Build** → `docs/website/build.ps1` runs a 10-step pipeline that compiles everything into `docs/website/.output/`.
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

## JSONata Benchmarks

The `benchmarks/Corvus.Text.Json.Jsonata.Benchmarks/` project compares the JSONata **code generator (CG)** against the **runtime compiler (RT)** and a **Jsonata.Net.Native** baseline across 20 expression categories. There are 62 benchmarks total (20 CG + 20 RT + 22 Native). If results show fewer than 62, something went wrong — see troubleshooting below.

### Building and running

```powershell
# 1. Build the code generator and runtime (must succeed before benchmarks)
dotnet build src\Corvus.Text.Json.Jsonata -c Release -v q --no-restore
dotnet build src\Corvus.Text.Json.Jsonata.CodeGeneration -c Release -v q --no-restore

# 2. Run conformance tests (always verify before benchmarking)
dotnet test tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests -f net10.0 --filter "category=codegen-conformance" --no-restore -v q

# 3. Clean stale BDN artifacts (CRITICAL — stale Job-* dirs cause file locks)
$benchDir = "benchmarks\Corvus.Text.Json.Jsonata.Benchmarks"
Remove-Item "$benchDir\bin\Release\net10.0\Job-*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$benchDir\BenchmarkDotNet.Artifacts\results\*" -Force -ErrorAction SilentlyContinue

# 4. Run benchmarks (takes ~15-25 minutes)
cd benchmarks\Corvus.Text.Json.Jsonata.Benchmarks
dotnet run -c Release -f net10.0 -- --filter '*'
```

### Critical rules

1. **Always clean `Job-*` directories** before running. BDN uses out-of-process toolchains that create `Job-*` subdirectories under `bin\Release\net10.0\`. Stale ones cause file locks; BDN's build exits with code 1 and **silently drops CodeGen benchmarks** from results. You won't see an error — you'll just get fewer results.
2. **Never pipe BDN output through `Select-Object -First N`** or any truncating command. This kills the BDN host process mid-run, producing incomplete/corrupt results.
3. **Use `mode="sync"` with `initial_wait=30`** when running from the Copilot shell. BDN runs for 15-25 minutes. After initial_wait expires it continues in background — you'll be notified on completion. Use `read_powershell` with `delay=600` (call twice if needed for the full output).
4. **Build timeout is pre-configured** in `Program.cs` at 15 minutes (`WithBuildTimeout(TimeSpan.FromMinutes(15))`). No `--buildTimeout` flag needed.
5. **Always pass `-- --filter '*'`** to run all benchmarks non-interactively. The `*` **must be single-quoted** in PowerShell to prevent glob expansion. Without quoting, PowerShell expands `*` to filenames, BDN receives no valid filter, and presents an interactive menu that blocks the shell.

### Result locations and naming

- Results are at `benchmarks/Corvus.Text.Json.Jsonata.Benchmarks/BenchmarkDotNet.Artifacts/results/` (**not** the repo root).
- JSON reports: `*-report-full.json` files, one per benchmark class.
- **Method naming convention:**
  - `Corvus_<Category>` → RT (runtime compiler)
  - `Corvus_CodeGen_<Category>` → CG (code generator)
  - `Native_<Category>` → Jsonata.Net.Native baseline
- **CG/RT ratio** = `CodeGen.Mean / Corvus.Mean`. CG WIN ≤ 0.95, RT WIN ≥ 1.05, PARITY otherwise.

### Parsing results (PowerShell)

```powershell
$base = "benchmarks\Corvus.Text.Json.Jsonata.Benchmarks\BenchmarkDotNet.Artifacts\results"
$files = Get-ChildItem $base -Filter "*Jsonata*-report-full.json"
foreach ($f in $files) {
    $report = Get-Content $f.FullName -Raw | ConvertFrom-Json
    foreach ($b in $report.Benchmarks) {
        $method = $b.Method; $mean = $b.Statistics.Mean
        # Group by category (from BenchmarkCategory attribute), compute CG/RT ratio
    }
}
```

### Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Fewer than 62 benchmarks in results | Stale `Job-*` dirs caused build failure | Clean `Job-*` dirs and re-run |
| BDN build exits code 1 | File lock from prior run | Clean `Job-*` dirs |
| No `*CodeGen*` methods in results | Source generator didn't run | Build in Release config, check `obj\Release\net10.0\generated\` for `.g.cs` files |
| Results in wrong directory | Looking at repo root | Check `benchmarks\...\BenchmarkDotNet.Artifacts\results\` |

## Namespaces

| Namespace | Purpose |
|---|---|
| `Corvus.Text.Json` | Public API |
| `Corvus.Text.Json.Internal` | Internal helpers, enumerators, metadata |
| `Corvus.Numerics` | `BigNumber`, `BigInteger` support |
| `Corvus.NodaTimeExtensions` | NodaTime (`LocalDate`, `OffsetDateTime`, `Period`) helpers |
