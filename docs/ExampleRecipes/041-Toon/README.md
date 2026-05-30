# JSON Schema Patterns in .NET - TOON Conversion

This recipe demonstrates how to convert between JSON and TOON using the `Corvus.Text.Json.Toon` package. It covers parsing TOON into a pooled Corvus document, converting TOON to JSON, converting JSON to TOON, dotted-key path expansion, JSON key folding, and writing TOON directly to a UTF-8 buffer.

## The Pattern

TOON is useful when you need a compact text representation of JSON-shaped data, especially for LLM prompts. It keeps the JSON data model, but removes repeated punctuation and can represent uniform object arrays as tables.

Use TOON at the prompt or transport boundary. Use JSON and generated Corvus types for validation, application logic, persistence, and API contracts.

## Parsing TOON to a Document

The Corvus package parses TOON into the same pooled document model used by the rest of `Corvus.Text.Json`:

```csharp
string toon = """
    name: Alice
    age: 30
    active: true
    scores[3]: 95,87,92
    """;

using ParsedJsonDocument<JsonElement> document = ToonDocument.Parse<JsonElement>(toon);
JsonElement root = document.RootElement;

Console.WriteLine($"Name:   {root.GetProperty("name").GetString()}");
Console.WriteLine($"Age:    {root.GetProperty("age").GetInt32()}");
Console.WriteLine($"Scores: {root.GetProperty("scores")}");
```

Output:

```text
Name:   Alice
Age:    30
Scores: [95,87,92]
```

## Converting TOON to JSON

Tabular TOON is converted back into standard JSON arrays of objects:

```csharp
string toon = """
    [2]{id,name,score}:
      1,Alice,95
      2,Bob,87
    """;

string json = ToonDocument.ConvertToJsonString(toon);
Console.WriteLine(json);
```

Output:

```json
[{"id":1,"name":"Alice","score":95},{"id":2,"name":"Bob","score":87}]
```

## Converting JSON to TOON

Uniform arrays of objects are emitted as TOON tables automatically:

```csharp
string json = """[{"id":1,"name":"Alice","score":95},{"id":2,"name":"Bob","score":87}]""";
string toon = ToonDocument.ConvertToToonString(json);
Console.WriteLine(toon);
```

Output:

```toon
[2]{id,name,score}:
  1,Alice,95
  2,Bob,87
```

## Expanding Dotted Keys

By default, dotted keys remain literal JSON property names. Set `ExpandPaths = ToonPathExpansion.Safe` when you want unambiguous dotted paths to become nested JSON objects:

```csharp
ToonReaderOptions options = new()
{
    ExpandPaths = ToonPathExpansion.Safe,
};

string json = ToonDocument.ConvertToJsonString(
    "user.name: Alice\nuser.age: 30",
    options);
```

JSON result:

```json
{"user":{"name":"Alice","age":30}}
```

## Folding JSON Keys

The reverse operation is key folding. Set `KeyFolding = ToonKeyFolding.Safe` and convert from a parsed `JsonElement` when you want the writer to fold nested object paths:

```csharp
ToonWriterOptions options = new()
{
    KeyFolding = ToonKeyFolding.Safe,
};

using ParsedJsonDocument<JsonElement> document =
    ParsedJsonDocument<JsonElement>.Parse("""{"user":{"name":"Alice"},"active":true}""");

JsonElement root = document.RootElement;
string toon = ToonDocument.ConvertToToon(in root, options);
```

TOON result:

```toon
user.name: Alice
active: true
```

## Writing to a UTF-8 Buffer

For hot paths, write TOON directly to an `IBufferWriter<byte>`:

```csharp
ArrayBufferWriter<byte> buffer = new(256);
ToonDocument.ConvertToToon(
    """[{"id":1,"name":"Alice","score":95},{"id":2,"name":"Bob","score":87}]"""u8,
    buffer);

ReadOnlySpan<byte> utf8Toon = buffer.WrittenSpan;
```

This avoids creating the intermediate TOON string. It is the preferred form when the next step writes to a stream, socket, or other UTF-8 boundary.

## Running the Example

```bash
cd docs/ExampleRecipes/041-Toon
dotnet run
```

## Related Patterns

- [027-Yaml](../027-Yaml/) — Convert between YAML and JSON
- [028-JsonCanonicalization](../028-JsonCanonicalization/) — Canonicalize JSON for stable hashes and signatures
- [028-JsonPath](../028-JsonPath/) — Query JSON with JSONPath
