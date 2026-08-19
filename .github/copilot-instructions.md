# Corvus.Text.Json — Agent Instructions

## Project Overview

**Corvus.Text.Json** is a high-performance JSON library for .NET extending `System.Text.Json` with pooled-memory parsing, JSON Schema validation (2019-09/2020-12), mutable document building, extended numerics (`BigNumber`, `BigInteger`), and NodaTime integration. A Roslyn incremental source generator and a CLI code generator (`corvusjson`) produce strongly-typed C# from JSON Schema. Repo structure mirrors dotnet/runtime conventions: shared source in `Common/`, polyfills from `System.Private.CoreLib/`, explicit `<Compile>` item groups (no globs) in select projects.

The low-level reader/writer/document types derive from dotnet/runtime's `System.Text.Json`; review upstream monthly and port bug/correctness fixes. Process and component mapping: `docs/UpstreamReview.md`.

## Skills

25 skills in `.github/skills/` carry the deep, task-specific context. Load the relevant one before working in its area; this file holds only what every session needs.

| Skill | Area |
|-------|------|
| `corvus-build-and-test` | Building, testing, TFM targeting, coverage methodology, test transcode helpers |
| `corvus-codegen` | Source generator and CLI code generation from JSON Schema |
| `corvus-keywords-and-validation` | JSON Schema keywords, vocabularies, validation handlers |
| `corvus-standalone-evaluator` | Validation-only evaluator generation and annotation collection |
| `corvus-parsed-documents-and-memory` | Parsing, IJsonElement, memory model, UTF-8 transcoding |
| `corvus-mutable-documents` | JsonWorkspace, JsonDocumentBuilder, mutation, JSON Patch |
| `corvus-buffer-and-pooling` | stackalloc/ArrayPool/ThreadStatic pooling patterns |
| `corvus-low-alloc-data-structures` | Ref-struct collections, SIMD, hash sets |
| `corvus-bytes-to-bytes` | Killing record↔document string seams; the genuine-leaf proof; the allocation self-audit |
| `corvus-builder-context-threading` | Building generated models from UTF-8 spans with no closure (`Build<TContext>`) |
| `corvus-ctj-handler-implementation` | OpenAPI server handlers with generated types; workspace-owned lifetimes |
| `corvus-typed-model-construction` | Allocation-free construction of generated models (Create/Build/CreateBuilder) |
| `corvus-numeric-types` | BigNumber, numeric parsing, format selection |
| `corvus-ecma-regex` | ECMAScript → .NET regex translation |
| `corvus-query-languages` | JSONata, JMESPath, JsonLogic, JSONPath |
| `corvus-yaml` | YAML ↔ JSON conversion |
| `corvus-analyzers` | Roslyn analyzers (CTJ001-CTJ010) |
| `corvus-benchmarks` | BenchmarkDotNet execution, B/C baseline convention, all benchmark projects |
| `corvus-docs-website` | Documentation site build pipeline and the playgrounds |
| `corvus-bowtie-testing` | Bowtie conformance testing against JSON Schema Test Suite |
| `corvus-test-suite-regeneration` | Regenerating test classes from the submodule |
| `corvus-v4-migration` | V4 → V5 migration patterns and analyzers |
| `corvus-xplat-dynamic-compilation` | Roslyn metadata references for cross-OS CI (build Ubuntu, test Windows net481) |
| `ref-struct-delegates` | Custom delegates for ref-struct parameters |
| `reviewing-skills` | Post-work review and maintenance of skills and instructions |

## Build & Test

```bash
dotnet build Corvus.Text.Json.slnx

dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop&TestCategory!=integration"

# Single class or method: add a FullyQualifiedName~ clause (substring match; ClassName= is unreliable here)
dotnet test --solution Corvus.Text.Json.slnx --filter "FullyQualifiedName~ParsedJsonDocumentTests&TestCategory!=failing&TestCategory!=outerloop&TestCategory!=integration"
```

- **Always** exclude `failing`, `outerloop`, and `integration` categories. `integration` needs Docker/Testcontainers (real brokers) and runs only where containers are available.
- Tests use **MSTest** (MSTest.Sdk, pinned in `global.json`) on **Microsoft Testing Platform**: `[TestMethod]`/`[DataRow]`/`[DynamicData]`/`[TestCategory]`; `Assert.ThrowsExactly<T>` (exact) vs `Assert.Throws<T>` (T-or-derived); static fields + `[ClassInitialize]`/`[ClassCleanup]` for fixtures. MTP rejects `--nologo` and `-v q`; use `--solution`/`--project` for targeting.
- `Corvus.Text.Json.slnx` is the main solution; `Corvus.Text.Json.Benchmarks.slnx` holds benchmarks. Test projects target `net10.0` and `net481`; omitting `-f` runs both. Multi-TFM quirks (net481 empty assemblies, Roslyn-hosted exclusions): see `corvus-build-and-test`.
- `TreatWarningsAsErrors=true` everywhere — any warning fails the build.

### Pre-commit gates (every commit, in order)

1. **Warning-free build**: `dotnet build Corvus.Text.Json.slnx` reports `0 Warning(s)`.
2. **Run affected tests end-to-end**: identify every test project exercising the changed code and run it. A build success is not evidence the change works. A pattern fixed across multiple sites means testing ALL of them.
3. **Code sample catalog**: if anything under `.github/`, `docs/`, or a skill/instruction file changed — first compile-verify any C# blocks in each changed file (the catalog only tracks line numbers; it cannot catch fabricated APIs), then:

   ```powershell
   .\docs\update-code-sample-catalog.ps1 -UpdateFile <relative-path>   # per changed file
   .\docs\update-code-sample-catalog.ps1 -Check                        # must exit 0
   ```

   This applies however the files changed (you, the user, accumulation across turns); CI fails on a stale catalog. Full workflow, triage rules, and script flags: `docs/CodeSampleCatalog.md`.
4. **Allocation & decision self-audit**: scan your diff and report, under a `Decisions & deferrals` heading in your message, every (a) managed `string`/`List<string>`/`Dictionary` on a path where bytes are available, (b) non-`static` builder lambda where a `static` + `TContext` form exists, (c) reflection-based dispatch, (d) work deferred or abandoned, (e) fix that moved a cost rather than removing it (before/after `file:line`). "Genuine leaf" / "marginal" / "admin-rare" / "low-frequency" / "pragmatic" are red flags requiring the two-ended proof in `corvus-bytes-to-bytes` — never justifications. Prove warm-path allocation claims with a `[MemoryDiagnoser]` baseline-vs-new benchmark.

### Diagnostic discipline

- **Measure before and after** — never claim an improvement without both numbers from the same command.
- **Replicate the failing environment** — cross-OS/TFM CI failures reproduce locally first (see `corvus-xplat-dynamic-compilation`); a fix passing under different conditions proves nothing.
- **No speculative fixes** — if you cannot reproduce it, say so; do not push and hope CI validates (CI runs are 30+ minutes and limited).
- **Diagnose before acting** — read the logs and identify the specific error or slow step before proposing anything.

### GitHub issues

Read the full issue before planning: body, labels, linked sub-issues, and the complete comment thread — comments often carry corrections and invariants that supersede the summary.

## Architecture

- **`IJsonDocument`** — pooled-memory documents; always `Dispose()` to return memory to `ArrayPool<byte>`.
- **`ParsedJsonDocument<T>`** — read-only parsed document. **`JsonDocumentBuilder<T>`** — mutable; version-tracked so stale element references throw.
- **`IJsonElement<T> where T : struct, IJsonElement<T>`** — CRTP interface every custom JSON type implements; shared traversal and schema API.
- **Partial-class organisation** — `JsonElement` splits by concern (`JsonElement.Parse.cs`, `.JsonSchema.cs`, `.Mutable.cs`, `JsonElementHelpers.*.cs`); add new concerns as new `JsonElement.<Concern>.cs` partials, never grow the core struct file.
- **Two codegen mechanisms**: the Roslyn `IIncrementalGenerator` (`src/Corvus.Text.Json.SourceGenerator/`, output inspectable in `obj/` via `EmitCompilerGeneratedFiles`) and the CLI (`corvusjson jsonschema`, `src/Corvus.Json.Cli.Core/`; the legacy `generatejsonschematypes` shim defaults to the V4 engine). **Never invent attribute or CLI option names** — verify against `IncrementalSourceGenerator.cs` and `GenerateCommand.cs` before writing docs or examples.
- **netstandard2.0** — polyfills are linked from `System.Private.CoreLib/src/` via conditional item groups; do not add package polyfills for what those cover.

## Buffer pattern

Temporary buffers stack-allocate small, rent large, always return in `finally`. Thresholds in `JsonConstants`: `StackallocByteThreshold` = 256 (bytes), `StackallocCharThreshold` = 128 (chars). Full rules, tiers, and thread-static caches: `corvus-buffer-and-pooling`.

```csharp
byte[]? rentedArray = null;
Span<byte> buffer = length <= JsonConstants.StackallocByteThreshold
    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
    : (rentedArray = ArrayPool<byte>.Shared.Rent(length));
try
{
    DoWork(buffer.Slice(0, length)); // rented arrays may be larger than requested
}
finally
{
    if (rentedArray != null)
    {
        ArrayPool<byte>.Shared.Return(rentedArray);
    }
}
```

## Key Conventions

- **Tone** — neutral benchmark language ("faster", "leads"); never "crush"/"destroy"/"kill"/"dominate".
- **`EnableDefaultCompileItems=false`** in: `Corvus.Text.Json`, `.Tests`, `.CodeGeneration`, `.SourceGenerator`, `.Compatibility`, and the four query-language source-generator projects — every `.cs` file needs an explicit `<Compile>` entry there. All other projects auto-discover.
- **`LangVersion=preview`** — prefer raw string literals (`"""`) for JSON; `"..."u8` where `ReadOnlySpan<byte>` is needed. **`AllowUnsafeBlocks=true`** in numeric/UTF-8 hot paths is expected.
- **Nullable** enabled in libraries, disabled in tests; public APIs need complete XML docs.
- **Shared source via `Common/`** — link with `<Compile Include="$(CommonPath)..." Link="..." />`, never duplicate.
- **`SR` alias** — all user-facing strings via `SR.Name` from the `.resx`; `using SR = Resources.Strings;` is a global using.
- **Suppressed project-wide**: `JSON001`, `CS8500`, `IDE0065`, `IDE0290` — no extra pragmas for these.
- **No trailing newline in `.cs` files** — SA1518 is an error; files end immediately after the final `}` with no `\n`.
- **JSON Schema test suite** — `JSON-Schema-Test-Suite/` is a submodule; update + regenerate via `.\update-json-schema-test-suite.ps1` (see `corvus-test-suite-regeneration`).
- **`BigNumber`** (in `Corvus.Numerics`) over `decimal` when precision may exceed 28 significant digits.
- **Test-first bug fixes** — write the reproducing test first; it fails before the fix and passes after, or production code does not change.
- **Coverage work** — target only uncovered lines identified in Cobertura XML, collect for ALL TFMs (never `-f net10.0` for baselines), verify target lines actually moved to >0 hits, use `dotnet-coverage` (never Coverlet). Full methodology and exclusion rules: `corvus-build-and-test`.
- **Doc samples** — prefer `ParsedJsonDocument<T>.Parse(...)` + `using` over `ParseValue`; use implicit `JsonElement.Source` conversions for scalars; import only `Corvus.Text.Json` (fully-qualify any `System.Text.Json` type).
- **Tests use Corvus types** — never `System.Text.Json` equivalents for code under test or assertions (STJ acceptable only as fixture-reading infrastructure).
- **Exact assertions** — `Assert.AreEqual` with the complete expected value (raw string literal), not `Contains`/`StartsWith`. Exceptions: error-message substrings, huge buffer-growth outputs, non-deterministic content. Capture actual output first, then pin it.

### Known doc-sample compilation traps

- `ParsedJsonDocument<T>.Parse("""..."""u8)` — `Parse` takes `ReadOnlyMemory<byte>`, not the span `u8` produces; drop the suffix.
- `ArrayBuilder.AddProperty()` does not exist — arrays use `AddItem()`; `AddProperty(name, value)` is `ObjectBuilder` only.
- `using System.Text.Json;` alongside `using Corvus.Text.Json;` makes `JsonElement`/`Utf8JsonWriter`/`JsonWriterOptions` ambiguous.

## JsonWorkspace (mutable documents)

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(json);
using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);
JsonElement.Mutable root = builder.RootElement;
// ... mutate ...
string result = root.ToString();
```

Always `using`; prefer `JsonWorkspace.Create()` (thread-local rented) over `CreateUnrented()` unless lifetime must escape the block. Full API incl. multi-builder, cloning, JSON Patch, and test helper types: `corvus-mutable-documents`.

## Task-area pointers

- **Documentation website** (build pipeline, generated-vs-authored files, incremental rebuilds, XmlDocToMarkdown) and **playgrounds** (running, ports, the WASM `SR.Format` gotcha): `corvus-docs-website` skill and `docs/website/DEVELOPMENT.md`. Stop any serving process before rebuilding — file locks hang the build.
- **Benchmarks**: B/ is the frozen baseline — **never regenerate B/**; regenerate all C/ after codegen changes (`pwsh benchmarks/scripts/Regenerate-CurrentBenchmarks.ps1`). BDN procedure, Job-* cleanup, result locations, JSONata specifics, and the full project table: `corvus-benchmarks` skill and `docs/BenchmarkGuide.md`. Confirm the machine is idle before running.
- **Code sample catalog**: full triage rules, first-pass prioritization, ExampleRecipes cross-referencing, and script flags: `docs/CodeSampleCatalog.md`. The pre-commit gate above is the mandatory minimum.

## Namespaces

| Namespace | Purpose |
|---|---|
| `Corvus.Text.Json` | Public API — core types, parsing, schema validation, JCS canonicalization |
| `Corvus.Text.Json.Internal` | Internal helpers, enumerators, metadata |
| `Corvus.Text.Json.Patch` | RFC 6902 JSON Patch, Merge Patch, Diff (`docs/JsonPatch.md`) |
| `Corvus.Text.Json.Jsonata` / `.JMESPath` / `.JsonLogic` / `.JsonPath` | Query/rule evaluators |
| `Corvus.Text.Json.Yaml` | YAML 1.2 ↔ JSON conversion |
| `Corvus.Text.Json.Validator` | Runtime dynamic schema validation via Roslyn (`docs/Validator.md`) |
| `Corvus.Text.Json.Compatibility` | V5 ↔ V4 ↔ `System.Text.Json` bridge for migration |
| `Corvus.Numerics` | `BigNumber`, `BigInteger` |
| `Corvus.NodaTimeExtensions` | NodaTime helpers |
