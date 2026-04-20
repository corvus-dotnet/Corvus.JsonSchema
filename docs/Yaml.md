# YAML to JSON Converter

> **[Try the YAML Playground](/playground-yaml/)** — convert YAML to JSON in your browser, with built-in examples covering all YAML features.

## Overview

`Corvus.Text.Json.Yaml` is a high-performance, zero-allocation YAML 1.2 to JSON converter built on a custom `ref struct` tokenizer that operates directly on UTF-8 bytes. It supports all YAML scalar styles (plain, single-quoted, double-quoted, literal block, folded block), flow and block collections, anchors and aliases, multi-document streams, and four schema modes. The converter produces JSON output via `Utf8JsonWriter`, integrating directly into the Corvus parsing pipeline.

Two packages are available:

| Package | Dependency | Use when |
|---------|-----------|----------|
| **`Corvus.Text.Json.Yaml`** | `Corvus.Text.Json` | You need `ParsedJsonDocument<T>` and the full Corvus document model |
| **`Corvus.Yaml.SystemTextJson`** | `System.Text.Json` only | You need a lightweight `JsonDocument` without Corvus dependencies |

Both packages share the same tokenizer and converter implementation via conditional compilation. They target `net9.0`, `net10.0`, `netstandard2.0`, and `netstandard2.1`.

## Conformance

The converter passes **100% of the JSON-testable cases** in the [yaml-test-suite](https://github.com/yaml/yaml-test-suite) — 279 valid and 94 error cases (373 of 402 total, 92.8%). The remaining 29 test cases exercise YAML features with no JSON equivalent (complex keys, empty keys, bare tags) and do not provide JSON reference output.

## Quick start

Install the package:

```bash
# Full Corvus document model
dotnet add package Corvus.Text.Json.Yaml

# Or: System.Text.Json only
dotnet add package Corvus.Yaml.SystemTextJson
```

### Corvus.Text.Json.Yaml

**Parse YAML to a typed document:**

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;

string yaml = """
    name: Alice
    age: 30
    hobbies:
      - reading
      - cycling
    """;

using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
JsonElement root = doc.RootElement;
Console.WriteLine(root.GetProperty("name").GetString()); // "Alice"
Console.WriteLine(root.GetProperty("age").GetInt32());    // 30
```

**Convert YAML to a JSON string:**

```csharp
string json = YamlDocument.ConvertToJsonString("key: value"u8.ToArray());
Console.WriteLine(json); // {"key":"value"}
```

**Stream YAML to a Utf8JsonWriter:**

```csharp
using var stream = new MemoryStream();
using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

YamlDocument.Convert("items:\n  - one\n  - two"u8, writer);
writer.Flush();

Console.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
```

### Corvus.Yaml.SystemTextJson

```csharp
using Corvus.Yaml;

string yaml = "name: Bob\nage: 25";

using JsonDocument doc = YamlDocument.Parse(yaml);
Console.WriteLine(doc.RootElement.GetProperty("name").GetString()); // "Bob"
```

## Configuration

The `YamlReaderOptions` struct controls parsing behavior:

```csharp
var options = new YamlReaderOptions
{
    Schema = YamlSchema.Core,                       // Core (default), Json, Failsafe, Yaml11
    DocumentMode = YamlDocumentMode.SingleRequired, // SingleRequired (default) or MultiAsArray
    DuplicateKeyBehavior = DuplicateKeyBehavior.Error, // Error (default) or LastWins
    MaxAliasExpansionDepth = 64,                    // Billion-laughs protection
    MaxAliasExpansionSize = 1_000_000,              // Max nodes from alias expansion
};

using var doc = YamlDocument.Parse<JsonElement>(yaml, options);
```

### Schemas

| Schema | Description |
|--------|-------------|
| `Core` | YAML 1.2 Core Schema (default). Recognizes `null`, `true`/`false`, integers (decimal, `0o77`, `0xFF`), floats (decimal, `.inf`, `.nan`). |
| `Json` | YAML 1.2 JSON Schema. Strict JSON-only: only `null`, `true`/`false`, and JSON-style numbers. |
| `Failsafe` | All scalars become JSON strings. No implicit type coercion. |
| `Yaml11` | YAML 1.1 compatibility. Adds `yes`/`no`/`on`/`off`/`y`/`n` booleans, sexagesimal integers, and merge keys (`<<`). |

### Document modes

| Mode | Description |
|------|-------------|
| `SingleRequired` | Expects exactly one YAML document. Throws `YamlException` if multiple documents are found. |
| `MultiAsArray` | Wraps all documents in a JSON array: `[doc1, doc2, ...]`. A single document still produces an array with one element. |

### Duplicate key handling

| Behavior | Description |
|----------|-------------|
| `Error` | Throws `YamlException` on duplicate mapping keys (default, per YAML spec recommendation). |
| `LastWins` | Last value for a duplicate key is kept (matches most YAML parsers' lenient behavior). |

## YAML features → JSON mapping

| YAML Feature | JSON Result |
|---|---|
| Mapping `key: value` | `{"key": "value"}` |
| Sequence `- item` | `["item"]` |
| Plain scalar `hello` | `"hello"` (or number/bool/null per schema) |
| Double-quoted `"hello\n"` | `"hello\n"` (escape sequences processed) |
| Single-quoted `'it''s'` | `"it's"` (only `''` → `'`) |
| Literal block `\|` | Preserves newlines |
| Folded block `>` | Folds newlines to spaces |
| Flow mapping `{a: 1}` | `{"a": 1}` |
| Flow sequence `[1, 2]` | `[1, 2]` |
| Anchor `&name value` | Stores for alias reference |
| Alias `*name` | Expands inline |
| Null `~`, `null`, empty | `null` |
| Bool `true`/`false` | `true`/`false` |
| Int `123`, `0o77`, `0xFF` | JSON number |
| Float `1.5`, `.inf`, `.nan` | Number, or `null` for infinity/NaN |
| Multi-document `---` | Array (in `MultiAsArray` mode) |
| Comment `# text` | Ignored |

## Anchors and aliases

YAML anchors (`&name`) and aliases (`*name`) are resolved inline — the aliased content is expanded into the JSON output:

```yaml
defaults: &defaults
  adapter: postgres
  host: localhost

development:
  <<: *defaults
  database: dev_db

production:
  <<: *defaults
  database: prod_db
```

Produces (with `YamlSchema.Yaml11` for merge key support):

```json
{
  "defaults": {"adapter": "postgres", "host": "localhost"},
  "development": {"adapter": "postgres", "host": "localhost", "database": "dev_db"},
  "production": {"adapter": "postgres", "host": "localhost", "database": "prod_db"}
}
```

### Billion-laughs protection

Alias expansion is bounded by `MaxAliasExpansionDepth` (default 64) and `MaxAliasExpansionSize` (default 1,000,000 nodes). Malicious YAML that attempts exponential expansion via nested aliases will be rejected with a `YamlException`.

## Block scalars

Literal (`|`) and folded (`>`) block scalars support chomping indicators:

```yaml
literal_keep: |+
  Line 1
  Line 2

folded_strip: >-
  This is a long
  paragraph that gets
  folded into one line.
```

| Indicator | Effect on trailing newlines |
|-----------|---------------------------|
| (none) | Clip: single trailing newline |
| `-` | Strip: no trailing newline |
| `+` | Keep: all trailing newlines preserved |

## Error handling

All parsing errors are reported as `YamlException` with line and column information:

```csharp
try
{
    using var doc = YamlDocument.Parse<JsonElement>(invalidYaml);
}
catch (YamlException ex)
{
    Console.WriteLine($"Line {ex.Line}, Column {ex.Column}: {ex.Message}");
}
```

## Code generator integration

The `generatejsonschematypes` CLI tool and the Roslyn source generator both accept `.yaml` and `.yml` files as schema input. YAML schemas are automatically converted to JSON before code generation:

**CLI:**

```bash
generatejsonschematypes schema.yaml \
    --rootNamespace MyApp.Models \
    --outputPath ./Generated
```

**Source generator:**

```csharp
[JsonSchemaTypeGenerator("schema.yaml")]
public partial class MySchema;
```

## Performance

The converter is designed for zero-allocation operation on the hot path. The `Utf8YamlScanner` is a `ref struct` that operates on `ReadOnlySpan<byte>` with `stackalloc`-based scratch buffers. Temporary buffers follow the standard `stackalloc`/`ArrayPool` pattern using `JsonConstants.StackallocByteThreshold` (256 bytes).

### Benchmarks vs YamlDotNet

The following benchmarks compare **Corvus.Yaml** (both `Corvus.Text.Json.Yaml` and `Corvus.Yaml.SystemTextJson`) against **YamlDotNet 16.3** for YAML→JSON conversion across nine scenarios of varying size and complexity.

> Measured with BenchmarkDotNet on .NET 10.0, Intel i7-13800H. YamlDotNet is configured with `JsonCompatible()` serialization. Full source and scenario files in [`benchmarks/Corvus.Text.Json.Yaml.Benchmarks/`](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/benchmarks/Corvus.Text.Json.Yaml.Benchmarks).

| Scenario | Size | YamlDotNet | Corvus (CTJ) | Corvus (STJ) | Speedup | YDN Alloc | Corvus Alloc | Alloc Ratio |
|---|---|---|---|---|---|---|---|---|
| SimpleScalar | 24 B | 5,027 ns | 245 ns | 255 ns | **20×** | 24,741 B | 136 B | **182×** |
| MultiDocument | 235 B | 31,097 ns | 1,800 ns | 1,879 ns | **17×** | 87,353 B | 136 B | **642×** |
| SmallConfig | 339 B | 34,416 ns | 2,458 ns | 2,514 ns | **14×** | 70,034 B | 136 B | **515×** |
| FlowStyle | 374 B | 61,161 ns | 3,859 ns | 3,953 ns | **16×** | 115,828 B | 136 B | **852×** |
| NestedMapping | 540 B | 54,090 ns | 3,974 ns | 4,072 ns | **14×** | 97,739 B | 136 B | **719×** |
| AnchorAlias | 604 B | 64,142 ns | 4,483 ns | 4,678 ns | **14×** | 106,984 B | 136 B | **787×** |
| BlockScalar | 642 B | 16,651 ns | 2,240 ns | 2,149 ns | **7×** | 30,767 B | 136 B | **226×** |
| ComplexMixed | 2.4 KB | 136,973 ns | 11,368 ns | 11,537 ns | **12×** | 186,505 B | 136 B | **1,372×** |
| LargeConfig | 6 KB | 413,192 ns | 30,478 ns | 31,268 ns | **14×** | 580,263 B | 136 B | **4,267×** |

**Summary:** Corvus.Yaml is **7–20× faster** than YamlDotNet and allocates **182–4,267× less memory**. The Corvus CTJ and STJ variants perform nearly identically — choose based on whether you need the Corvus document model or just `System.Text.Json.JsonDocument`.

## Comparison with YamlDotNet

[YamlDotNet](https://github.com/aaubry/YamlDotNet) is the established YAML library for .NET, with a large feature set and broad ecosystem adoption. Corvus.Yaml takes a different approach — it is a purpose-built, high-performance YAML→JSON converter rather than a general-purpose YAML toolkit.

### Feature comparison

| Feature | Corvus.Yaml | YamlDotNet |
|---|---|---|
| **YAML→JSON conversion** | ✅ Native, zero-copy | ⚠️ Via deserialize + `JsonCompatible()` serialize |
| **YAML 1.2 conformance** | ✅ 100% of JSON-testable cases (373/402) | ⚠️ 68.9% JSON conformance (257/373) [*](#conformance-note) |
| **YAML 1.1 support** | ✅ `YamlSchema.Yaml11` mode | ✅ Native |
| **YAML emitting (C# → YAML)** | ❌ | ✅ Full emitter |
| **Object serialization/deserialization** | ❌ | ✅ `Serializer`/`Deserializer` with naming conventions, type converters, callbacks |
| **Object model (DOM)** | ❌ | ✅ `YamlStream`/`YamlDocument`/`YamlNode` tree |
| **Low-level event parser** | ❌ | ✅ `IParser` with `StreamStart`, `DocumentStart`, `Scalar`, etc. |
| **Schema modes** | ✅ Core, JSON, Failsafe, YAML 1.1 | ⚠️ 1.1 by default; configurable |
| **Duplicate key handling** | ✅ Configurable (`Error` / `LastWins`) | ✅ Configurable |
| **Anchors & aliases** | ✅ With billion-laughs protection | ✅ |
| **Multi-document streams** | ✅ `MultiAsArray` mode | ✅ Via `IParser` iteration |
| **Block scalars (`\|`, `>`)** | ✅ All chomping indicators | ✅ |
| **Performance** | ✅ 7–20× faster, near-zero allocation | ⚠️ Higher allocation, string-based |
| **`System.Text.Json` integration** | ✅ Direct `JsonDocument` / `ParsedJsonDocument` output | ❌ Separate ecosystem |
| **Corvus document model** | ✅ `ParsedJsonDocument<T>`, JSON Schema validation | ❌ |
| **Code generator integration** | ✅ YAML schemas in `generatejsonschematypes` | ❌ |
| **Target frameworks** | net9.0, net10.0, netstandard2.0/2.1 | net8.0, net10.0, net47, netstandard2.0/2.1 |
| **NuGet downloads** | New | ~350M+ |
| **Unity support** | ❌ | ✅ Unity Asset Store |
| **License** | Apache 2.0 | MIT |

<a id="conformance-note"></a>*Conformance figures from the [yaml-test-matrix](https://matrix.yaml.info/) (Jan 2022 data). YamlDotNet may have improved since.*

### When to use Corvus.Yaml

- **YAML configuration → JSON pipeline:** You need to convert YAML files (Kubernetes manifests, CI configs, OpenAPI specs) into JSON for processing by `System.Text.Json`, JSON Schema validation, or the Corvus document model.
- **High-throughput / low-allocation:** You're processing YAML in a hot path — an API server, a build tool, a code generator — where 7–20× speed and near-zero GC pressure matter.
- **Spec conformance:** You need accurate YAML 1.2 parsing with correct type resolution (Core schema) and strict error detection.
- **Corvus ecosystem:** You're already using Corvus.Text.Json for JSON Schema, JSONata, JMESPath, or JSON Patch and want YAML input to flow into the same pipeline.

### When to use YamlDotNet

- **YAML round-tripping:** You need to read YAML, modify it, and write it back as YAML — Corvus.Yaml is read-only (YAML→JSON only).
- **Object serialization:** You need to serialize C# objects directly to/from YAML with naming conventions, type converters, and custom deserialization callbacks.
- **YAML DOM manipulation:** You need to build or transform a YAML document tree (`YamlStream`, `YamlNode`) programmatically.
- **Event-level parsing:** You need low-level control over the YAML parse stream (e.g., custom document handling, streaming large files node-by-node).
- **Ecosystem compatibility:** You're using a library that depends on YamlDotNet (e.g., KubernetesClient, NJsonSchema.Yaml).

### Using both together

The two libraries are not mutually exclusive. A common pattern is to use YamlDotNet for its serialization/deserialization features and Corvus.Yaml for high-performance YAML→JSON conversion in the same project:

```csharp
// Use Corvus.Yaml for fast YAML→JSON conversion and schema validation
using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yamlBytes);
bool isValid = doc.RootElement.IsValid(mySchema);

// Use YamlDotNet for object deserialization with naming conventions
var deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();
var config = deserializer.Deserialize<AppConfig>(yamlString);
```

## API reference

- [`Corvus.Text.Json.Yaml` API docs](/api/v5/corvus-text-json-yaml.html)
- [`Corvus.Yaml.SystemTextJson` API docs](/api/v5/corvus-yaml-system-text-json.html)
