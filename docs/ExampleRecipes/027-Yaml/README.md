# JSON Schema Patterns in .NET - YAML to JSON Conversion

This recipe demonstrates how to convert YAML content to JSON using the `Corvus.Text.Json.Yaml` library. It covers basic parsing, schema modes, multi-document handling, anchors and aliases, block scalars, and streaming conversion.

## The Pattern

YAML is a human-friendly data serialization format commonly used for configuration files, CI pipelines, Kubernetes manifests, and API specifications. The `Corvus.Text.Json.Yaml` converter transforms YAML content into JSON with zero-allocation on the hot path, supporting all YAML 1.2 features and four schema modes.

## Parsing YAML to a Document

The simplest approach parses YAML to a strongly-typed `ParsedJsonDocument<JsonElement>`:

```csharp
string yaml = """
    name: Alice
    age: 30
    hobbies:
      - reading
      - cycling
    """;

using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
JsonElement root = doc.RootElement;
Console.WriteLine($"Name: {root.GetProperty("name").GetString()}");
Console.WriteLine($"Age:  {root.GetProperty("age").GetInt32()}");
```

Output:
```
Name: Alice
Age:  30
```

## Converting to a JSON String

When you need the raw JSON text:

```csharp
string json = YamlDocument.ConvertToJsonString("key: value");
// {"key":"value"}
```

## Schema Modes

The YAML Core Schema (default) resolves plain scalars to typed JSON values:

```yaml
string: hello
integer: 42
hex: 0xFF
octal: 0o77
float: 3.14
infinity: .inf
not_a_number: .nan
null_value: null
tilde_null: ~
boolean: true
```

The JSON Schema mode is stricter — only JSON-compatible patterns are recognized as typed values. The Failsafe Schema treats everything as strings. YAML 1.1 compatibility mode adds `yes`/`no`/`on`/`off` booleans.

## Anchors and Aliases

YAML anchors define reusable content; aliases reference it:

```yaml
defaults: &defaults
  adapter: postgres
  host: localhost

development:
  database: dev_db
  <<: *defaults
```

With `YamlSchema.Yaml11`, the merge key (`<<`) expands the anchor inline.

## Block Scalars

Literal blocks (`|`) preserve newlines; folded blocks (`>`) join lines:

```yaml
literal: |
  Line 1
  Line 2

folded: >
  This is a long
  paragraph.
```

Chomping indicators (`-` strip, `+` keep) control trailing newlines.

## Multi-Document Streams

YAML files can contain multiple documents separated by `---`:

```yaml
---
name: first
---
name: second
```

Use `YamlDocumentMode.MultiAsArray` to parse all documents into a JSON array.

## Event Parsing

For advanced scenarios, `EnumerateEvents` provides zero-allocation, callback-based event enumeration:

```csharp
YamlDocument.EnumerateEvents(
    "items:\n  - one\n  - two",
    (in YamlEvent e) =>
    {
        Console.WriteLine($"[{e.Line}:{e.Column}] {e.Type}");
        return true; // return false to stop early
    });
```

The `YamlEvent` ref struct provides the event type, scalar value (UTF-8 bytes), anchor, tag, scalar style, and line/column position. Its spans point directly into the source buffer and are only valid during the callback.

## Running the Example

```bash
cd docs/ExampleRecipes/027-Yaml
dotnet run
```

## Related Patterns

- [023-JsonPatch](../023-JsonPatch/README.md) — Apply RFC 6902 patches to JSON documents
- [024-JsonLogic](../024-JsonLogic/README.md) — Evaluate JsonLogic rules against JSON data
- [025-Jsonata](../025-Jsonata/README.md) — Query and transform JSON with JSONata expressions
- [026-Jmespath](../026-Jmespath/README.md) — Query JSON with JMESPath expressions
