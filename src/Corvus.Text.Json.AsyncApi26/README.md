# Corvus.Text.Json.AsyncApi26

Strongly-typed V5 model types for AsyncAPI 2.6 specifications.

Generated from the official [AsyncAPI 2.6.0 JSON Schema metaschema](https://github.com/asyncapi/spec-json-schemas)
using the Corvus.Text.Json code generator.

## Usage

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi26;

using ParsedJsonDocument<AsyncApiDocument> doc =
    ParsedJsonDocument<AsyncApiDocument>.Parse(File.ReadAllBytes("my-api.json"));

AsyncApiDocument root = doc.RootElement;
string version = (string)root.Asyncapi; // "2.6.0"
```

## AsyncAPI 2.x Structure

In AsyncAPI 2.x, operations are embedded in channels as `publish`/`subscribe`:

- `channels` → map of channel items, each with optional `publish` and `subscribe` operations
- `servers` → map of server objects
- `components` → reusable schemas, messages, security schemes, etc.