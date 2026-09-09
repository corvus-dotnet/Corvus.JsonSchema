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

## Regenerating

Build the CLI in Release, then run from the repository root:

```powershell
dotnet build src/Corvus.Json.Cli -f net10.0 -c Release
dotnet src/Corvus.Json.Cli/bin/Release/net10.0/Corvus.Json.Cli.dll jsonschema AsyncApi-Spec-Schemas/2.6.0.json --rootNamespace Corvus.Text.Json.AsyncApi26 --outputRootTypeName AsyncApiDocument --outputPath src/Corvus.Text.Json.AsyncApi26/Generated
```

Delete the contents of `Generated/` first so that files the generator no longer produces are removed.
