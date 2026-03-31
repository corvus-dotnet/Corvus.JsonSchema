# Architecture

This document provides a high-level overview of the Corvus.JsonSchema system architecture.

## System layers

```
┌─────────────────────────────────────────────────────────┐
│                    User Applications                    │
├──────────────┬──────────────┬───────────────────────────┤
│  CLI Tool    │  Source      │  Validator                │
│  (codegen)   │  Generator   │  (runtime)                │
├──────────────┴──────────────┴───────────────────────────┤
│              Code Generation Engine                     │
│  (Keywords, Vocabularies, TypeDeclaration, Reduction)   │
├─────────────────────────────────────────────────────────┤
│              Runtime Library                            │
│  (ParsedJsonDocument, JsonDocumentBuilder, Evaluation)  │
├─────────────────────────────────────────────────────────┤
│              System.Text.Json (BCL)                     │
└─────────────────────────────────────────────────────────┘
```

### Runtime library (`Corvus.Text.Json`)

The core library providing:

- **`ParsedJsonDocument<T>`** — immutable, pooled-memory JSON document parsed from UTF-8
- **`JsonDocumentBuilder<T>`** — mutable JSON document with version tracking
- **`IJsonElement<T>`** — CRTP-style interface for custom JSON element types
- **`JsonSchemaResultsCollector`** — collects schema validation results at various verbosity levels
- **`JsonSchemaAnnotationProducer`** — extracts annotations from verbose validation results
- **`Utf8JsonReader`** / **`Utf8JsonWriter`** — high-performance UTF-8 JSON I/O (forked from dotnet/runtime)
- **Format validation** — URI, date-time, email, hostname, regex, and numeric format validators

Targets: `net8.0`, `net9.0`, `net10.0`, `netstandard2.0`

### Code generation engine (`Corvus.Text.Json.CodeGeneration`)

The schema analysis and code emission layer:

- **Keywords** — `IKeyword` implementations for every JSON Schema keyword across all drafts
- **Vocabularies** — keyword groups per draft (4, 6, 7, 2019-09, 2020-12)
- **TypeDeclaration** — the central data structure representing a resolved JSON Schema as a type
- **Type reduction** — collapses trivial subschemas into their parents for leaner generated code
- **Validation handlers** — emit C# validation code from keyword constraints
- **Standalone evaluator generator** — emits a single static evaluator class

### Source generator (`Corvus.Text.Json.SourceGenerator`)

A Roslyn incremental generator triggered by `[JsonSchemaTypeGenerator]` attributes. It shares source files with the code generation engine via MSBuild `<Compile>` links. Output is written to `obj/` (`EmitCompilerGeneratedFiles=true`).

### CLI tool (`Corvus.Json.CodeGenerator`)

The `generatejsonschematypes` command-line tool generates C# from JSON Schema files. Used for:
- Pre-generating models (test projects, benchmark models)
- CI/CD pipelines
- Scenarios where source generator integration isn't suitable

### Validator (`Corvus.Json.Validator`)

A standalone schema validation tool that uses the standalone evaluator for command-line JSON validation.

## The IJsonElement&lt;T&gt; CRTP pattern

The library uses the Curiously Recurring Template Pattern (CRTP) for its element types:

```csharp
public interface IJsonElement<T> where T : struct, IJsonElement<T>
{
    // Element operations...
}

public readonly partial struct Person : IJsonElement<Person>
{
    // Person-specific operations...
}
```

This enables:
- Static dispatch (no virtual calls) for element operations
- Type-safe element traversal and conversion
- Generated types that share the same traversal and validation infrastructure

## Partial-class organisation

Large types are split across partial files by concern:

```
JsonElement.cs              — Core struct definition
JsonElement.Parse.cs        — Parsing methods
JsonElement.JsonSchema.cs   — Schema validation
JsonElement.Mutable.cs      — Mutable document operations
JsonElementHelpers.*.cs     — Domain-specific helpers (DateTime, Uri, etc.)
```

Generated types follow the same pattern:
```
Person.cs                   — Type definition, constructors
Person.Properties.cs        — Property accessors
Person.Validate.cs          — Validation code
Person.Conversions.cs       — Type conversions
```

## Shared source (`Common/`)

Files in `Common/src/` and `Common/tests/` are shared across projects via MSBuild links:

```xml
<Compile Include="$(CommonPath)Corvus\Text\Json\Reader\JsonReaderHelper.cs"
         Link="Common\JsonReaderHelper.cs" />
```

This mirrors the dotnet/runtime convention. Shared code includes:
- JSON reader/writer helpers
- UTF-8 transcoding utilities
- Numeric parsing helpers
- Test infrastructure

## Polyfill strategy (`System.Private.CoreLib/`)

For `netstandard2.0` compatibility, polyfill source files are linked from `System.Private.CoreLib/src/`:
- Nullable attributes
- `CallerArgumentExpressionAttribute`
- `Index` / `Range` types
- `StackExtensions`

These are conditionally included:
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <Compile Include="$(CoreLibPath)System\Index.cs" Link="Polyfills\Index.cs" />
</ItemGroup>
```

Do not add NuGet polyfill packages for features already covered by these linked files.

## Memory management

### Pooled documents

`ParsedJsonDocument<T>` and `JsonDocumentBuilder<T>` rent memory from `ArrayPool<byte>`. Always dispose them:

```csharp
using var doc = ParsedJsonDocument<Person>.Parse(json);
```

### JsonWorkspace

`JsonWorkspace` is a scoped container for pooled memory during mutable operations. It manages multiple `JsonDocumentBuilder<T>` instances and `Utf8JsonWriter` rentals:

```csharp
using var workspace = JsonWorkspace.Create();  // Rented from thread-local cache
using var builder = doc.RootElement.BuildDocument(workspace);
```

### Stack allocation pattern

Temporary buffers use `stackalloc` for small sizes, `ArrayPool.Rent` for larger:

```csharp
byte[]? rentedArray = null;
Span<byte> buffer = length <= JsonConstants.StackallocByteThreshold
    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
    : (rentedArray = ArrayPool<byte>.Shared.Rent(length));
try { /* use buffer */ }
finally { if (rentedArray != null) ArrayPool<byte>.Shared.Return(rentedArray); }
```

## Further reading

| Topic | Document |
|-------|----------|
| Code generation patterns | [CodeGenerationPatternDiscovery.md](docs/CodeGenerationPatternDiscovery.md) |
| Standalone evaluator | [StandaloneEvaluatorInternals.md](docs/StandaloneEvaluatorInternals.md) |
| Validation handlers | [ValidationHandlerGuide.md](docs/ValidationHandlerGuide.md) |
| Annotation system | [AnnotationSystem.md](docs/AnnotationSystem.md) |
| Adding keywords | [AddingKeywords.md](docs/AddingKeywords.md) |
| Numeric types | [NumericTypes.md](docs/NumericTypes.md) |
| Benchmarks | [BenchmarkGuide.md](docs/BenchmarkGuide.md) |
| Migration from V4 | [MigratingFromV4ToV5.md](docs/MigratingFromV4ToV5.md) |
| Parsed documents | [ParsedJsonDocument.md](docs/ParsedJsonDocument.md) |
| Mutable documents | [JsonDocumentBuilder.md](docs/JsonDocumentBuilder.md) |
| Schema evaluator | [SchemaEvaluator.md](docs/SchemaEvaluator.md) |
| Release process | [ReleaseProcess.md](docs/ReleaseProcess.md) |