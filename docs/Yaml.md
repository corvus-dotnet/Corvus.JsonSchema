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

The converter passes **all 373 tests** in the [yaml-test-suite](https://github.com/yaml/yaml-test-suite) that provide expected output (279 valid + 94 error), achieving 100% conformance against the YAML 1.2 specification. A further 29 test cases in the suite provide only parse-event data (no JSON reference output) and are not covered by the automated tests.

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

## API reference

- [`Corvus.Text.Json.Yaml` API docs](/api/v5/corvus-text-json-yaml.html)
- [`Corvus.Yaml.SystemTextJson` API docs](/api/v5/corvus-yaml-system-text-json.html)
