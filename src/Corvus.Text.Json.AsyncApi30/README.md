# Corvus.Text.Json.AsyncApi30

Strongly-typed V5 model types for [AsyncAPI 3.0](https://www.asyncapi.com/docs/reference/specification/v3.0.0) specifications.

## Overview

This package provides a complete, strongly-typed C# representation of AsyncAPI 3.0 documents, generated from the official AsyncAPI 3.0 JSON Schema metaschema using the Corvus.Text.Json source generator.

The root type is `AsyncApiDocument`, which gives you typed access to:

- `Servers` — server definitions with protocol bindings
- `Channels` — reusable channel definitions
- `Operations` — top-level operation definitions with `action` (send/receive)
- `Components` — reusable schemas, messages, channels, operations, security schemes
- `Info` — API metadata (title, version, description, contact, license)

## Usage

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi30;

using ParsedJsonDocument<AsyncApiDocument> doc = ParsedJsonDocument<AsyncApiDocument>.Parse(specJson);
AsyncApiDocument root = doc.RootElement;

// Access operations
foreach (var op in root.Operations.EnumerateObject())
{
    Console.WriteLine($"Operation: {op.Name} Action: {op.Value.Action}");
}
```

## License

Apache-2.0