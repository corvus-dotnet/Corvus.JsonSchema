# TOON converter

> **[Try the TOON playground](../playground-toon/)** - convert between JSON and TOON in the browser.

## Overview

TOON (Token-Oriented Object Notation) is a compact text representation for JSON-shaped data. It keeps the JSON data model, but removes some repeated punctuation and object member names. Nested objects use indentation. Uniform arrays of objects can be written as tables.

The Corvus TOON packages support both directions:

| Direction | Supported |
|---|---|
| TOON to JSON | Yes |
| TOON to a parsed document | Yes |
| JSON to TOON | Yes |
| JSON element to TOON | Yes |
| UTF-8 buffer APIs | Yes |

Use TOON at boundaries where you need to pass structured data to an LLM and want to reduce token count. Keep JSON for storage, APIs, validation, and general application contracts.

The best case for TOON is a uniform array of objects. For example, this JSON:

```json
[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]
```

can be written as:

```toon
[2]{id,name}:
  1,Alice
  2,Bob
```

That removes repeated property names without losing the row/field structure.

## Packages

| Package | Dependency | Use when |
|---|---|---|
| **`Corvus.Text.Json.Toon`** | `Corvus.Text.Json` | You want `ParsedJsonDocument<T>` and the Corvus document model. |
| **`Corvus.Toon.SystemTextJson`** | `System.Text.Json` only | You want TOON conversion without a dependency on `Corvus.Text.Json`. |

Both packages use the same converter implementation via conditional compilation. They target `net9.0`, `net10.0`, `netstandard2.0`, and `netstandard2.1`.

## Related resources

| Resource | Link |
|---|---|
| TOON site | [toonformat.dev](https://toonformat.dev/) |
| Reference implementation | [github.com/toon-format/toon](https://github.com/toon-format/toon) |
| Specification | [github.com/toon-format/spec](https://github.com/toon-format/spec) |
| Specification document | [SPEC.md](https://github.com/toon-format/spec/blob/main/SPEC.md) |
| LLM guide | [Using TOON with LLMs](https://toonformat.dev/guide/llm-prompts.html) |
| Benchmarks | [TOON benchmarks](https://toonformat.dev/guide/benchmarks.html) |
| Example recipe | [041-Toon](../ExampleRecipes/041-Toon/) |
| Playground | [TOON playground](../playground-toon/) |

## Corvus and Cysharp

Cysharp's [`ToonEncoder`](https://github.com/Cysharp/Toon) is the established .NET package for encoding `System.Text.Json` values to TOON, particularly when you want serializer/POCO integration. Corvus takes a different approach: it is a bidirectional JSON-shaped data converter, with UTF-8-first APIs and separate packages for the Corvus document model and plain `System.Text.Json`.

The key distinction is that Cysharp is an encoder, while the Corvus packages are converters. If you need to consume TOON and get JSON back out, use Corvus. If you only need to serialize POCOs or `System.Text.Json.JsonElement` values to TOON, Cysharp may be the simpler fit.

### Feature comparison

| Feature | `Corvus.Text.Json.Toon` | `Corvus.Toon.SystemTextJson` | Cysharp `ToonEncoder` |
|---|---|---|---|
| **TOON to JSON** | ✅ `ConvertToJsonString`, `Parse<T>`, `Utf8JsonWriter` | ✅ `ConvertToJsonString`, `Parse`, `Utf8JsonWriter` | ❌ |
| **JSON to TOON** | ✅ UTF-8 JSON, string JSON, Corvus `JsonElement` | ✅ UTF-8 JSON, string JSON, STJ `JsonElement` | ✅ STJ `JsonElement`, serializer input |
| **`System.Text.Json` support** | ⚠️ JSON input/output; use the STJ package for `JsonElement` | ✅ Native `JsonDocument` / `JsonElement` support | ✅ Native `JsonElement` and serializer support |
| **Corvus support** | ✅ `ParsedJsonDocument<T>`, Corvus `JsonElement`, generated model integration | ❌ | ❌ |
| **POCO support** | ❌ | ❌ | ✅ Direct serializer-based encoding |

### When to choose each package

- **Choose `Corvus.Text.Json.Toon`** when TOON is part of a Corvus pipeline: JSON Schema validation, generated models, JSONata/JMESPath/JsonPath processing, or pooled `ParsedJsonDocument<T>` lifetimes. This is the package to use when TOON should feed the same document model as the rest of Corvus.Text.Json.
- **Choose `Corvus.Toon.SystemTextJson`** when you want TOON to JSON and JSON to TOON without taking a dependency on the Corvus document model. It is the right fit for applications that are otherwise built around `System.Text.Json.JsonDocument` and `JsonElement`.
- **Choose Cysharp `ToonEncoder`** when your requirement is strictly JSON to TOON and you want direct POCO/serializer integration. It is not a decoder, so it is not suitable when TOON is an input format you need to parse.

### Benchmarks

The following benchmarks compare the directly comparable JSON to TOON paths against Cysharp, using the 100-row person-array scenario. This is the case TOON is intended to handle well: repeated object shapes where the field list can be written once and row values streamed compactly.

> Measured with BenchmarkDotNet on .NET 10.0, Intel i7-13800H. Full source is in [`benchmarks/Corvus.Text.Json.Toon.Benchmarks/`](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/benchmarks/Corvus.Text.Json.Toon.Benchmarks). Decode is Corvus-only because Cysharp has no TOON to JSON API.

#### JSON to TOON

| Scenario | Cysharp path | Cysharp | Corvus path | Corvus | Speedup | Cysharp Alloc | Corvus Alloc | Alloc Ratio |
|---|---|---:|---:|---|---:|---:|---:|---:|
| JSON element to TOON string | `JsonElement` encoder | 35.52 us | Generated model encoder | 23.36 us | **1.52×** | 4,416 B | 3,744 B | **1.18×** |
| JSON element to TOON UTF-8 buffer | `JsonElement` encoder | 39.57 us | Generated model encoder | 22.74 us | **1.74×** | 648 B | 0 B | **∞** |
| Tabular JSON element to TOON string | Tabular `JsonElement` encoder | 24.38 us | Generated model encoder | 23.36 us | **1.04×** | 4,136 B | 3,744 B | **1.10×** |
| Tabular JSON element to TOON UTF-8 buffer | Tabular `JsonElement` encoder | 28.09 us | Generated model encoder | 22.74 us | **1.24×** | 368 B | 0 B | **∞** |

*Speedup is Cysharp mean ÷ Corvus mean. Alloc Ratio is Cysharp allocation ÷ Corvus allocation; `∞` means Corvus allocated 0 B in that benchmark.*

**Summary:** Corvus is **1.04-1.74× faster** on these JSON element encode paths. Where both implementations allocate, Corvus allocates **1.10-1.18× less memory**. The UTF-8 buffer paths are the more important distinction: Corvus writes TOON with **0 B/op** in this benchmark.

#### TOON to JSON

Cysharp has no TOON decoder, so there is no direct external comparison for this table. The useful comparison is between the Corvus string-returning path and the writer path. The writer path avoids allocating the JSON output string; callers provide the destination.

| Scenario | Corvus string output | Corvus string alloc | Corvus UTF-8 writer | Corvus writer alloc | Writer speedup | Alloc Ratio |
|---|---:|---:|---:|---:|---:|---:|
| TOON to JSON | 35.96 us | 7,848 B | 33.29 us | 0 B | **1.08×** | **∞** |

**Summary:** TOON decode is currently a Corvus-only capability. Use the string-returning API when you need a `string`. Use the UTF-8 writer overload in hot paths or streaming code; it avoids the output string allocation and measured **0 B/op** here.

## Installation

```bash
# Full Corvus document model
dotnet add package Corvus.Text.Json.Toon

# System.Text.Json-only package
dotnet add package Corvus.Toon.SystemTextJson
```

## Quick start

### Corvus.Text.Json.Toon

Parse TOON into a Corvus document:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Toon;

string toon = """
    name: Alice
    age: 30
    active: true
    """;

using ParsedJsonDocument<JsonElement> document = ToonDocument.Parse<JsonElement>(toon);
JsonElement root = document.RootElement;

string? name = root.GetProperty("name").GetString();
int age = root.GetProperty("age").GetInt32();
```

Convert TOON to a JSON string:

```csharp
using Corvus.Text.Json.Toon;

string json = ToonDocument.ConvertToJsonString("name: Alice\nage: 30");
```

Convert JSON to TOON:

```csharp
using Corvus.Text.Json.Toon;

string toon = ToonDocument.ConvertToToonString(
    """{"name":"Alice","age":30,"active":true}"""u8);
```

### Corvus.Toon.SystemTextJson

```csharp
using Corvus.Toon;

using System.Text.Json.JsonDocument document =
    ToonDocument.Parse("name: Bob\nage: 25");

System.Text.Json.JsonElement root = document.RootElement;
string? name = root.GetProperty("name").GetString();
```

## TOON syntax supported by the converter

### Objects

Objects use `key: value` pairs. Indentation represents nested objects.

```toon
name: Alice
profile:
  age: 30
  active: true
```

JSON result:

```json
{"name":"Alice","profile":{"age":30,"active":true}}
```

### Inline arrays

Inline arrays declare their item count in the header.

```toon
[3]: 1,true,Alice
```

JSON result:

```json
[1,true,"Alice"]
```

The delimiter can be comma, pipe, or tab. For example:

```toon
[3|]: 1|true|Alice
```

### Tabular arrays

Uniform arrays of objects use the table form.

```toon
[2]{id,name}:
  1,Alice
  2,Bob
```

JSON result:

```json
[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]
```

When converting JSON to TOON, uniform object arrays are emitted as tables automatically.

## Reader options

`ToonReaderOptions` controls TOON decoding.

```csharp
using Corvus.Text.Json.Toon;

ToonReaderOptions options = new()
{
    Strict = false,
    IndentSize = 2,
    ExpandPaths = ToonPathExpansion.Safe,
};

string json = ToonDocument.ConvertToJsonString("[2]: 1", options);
```

| Option | Default | Description |
|---|---|---|
| `Strict` | `true` | Checks declared array counts and duplicate object keys. |
| `IndentSize` | `2` | Number of spaces per indentation level. |
| `ExpandPaths` | `Off` | Expands dotted keys, such as `user.name`, into nested JSON objects when set to `Safe`. |

## Writer options

`ToonWriterOptions` controls JSON to TOON encoding.

```csharp
using Corvus.Text.Json.Toon;

ToonWriterOptions options = new()
{
    Delimiter = ToonDelimiter.Pipe,
    KeyFolding = ToonKeyFolding.Safe,
    FlattenDepth = 4,
};

string toon = ToonDocument.ConvertToToonString(
    """{"user":{"name":"Alice"},"active":true}"""u8,
    options);
```

| Option | Default | Description |
|---|---|---|
| `IndentSize` | `2` | Number of spaces per indentation level. |
| `Delimiter` | `Comma` | Delimiter for inline arrays, table headers, and table rows (`Comma`, `Pipe`, or `Tab`). |
| `KeyFolding` | `Off` | Folds nested object paths when set to `Safe`. |
| `FlattenDepth` | `int.MaxValue` | Maximum number of path segments to fold when key folding is enabled. |

## Error handling

Invalid TOON input throws `ToonException`. The exception message includes the 1-based line and column where parsing failed.

```csharp
using Corvus.Text.Json.Toon;

try
{
    ToonDocument.ConvertToJsonString("[2]: 1");
}
catch (ToonException ex)
{
    string message = ex.Message;
}
```

## Performance notes

Prefer the UTF-8 overloads when your input is already UTF-8. The converter works in UTF-8 internally and uses stack-backed buffers first, with `ArrayPool<T>` fallback for larger temporary values.

```csharp
using Corvus.Text.Json.Toon;

string toon = ToonDocument.ConvertToToonString("""{"id":1,"name":"Alice"}"""u8);
```
