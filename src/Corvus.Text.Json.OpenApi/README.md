# Corvus.Text.Json.OpenApi

Shared interfaces and types for walking OpenAPI and AsyncAPI specifications,
extracting schemas for code generation.

## Key Types

- **`ISpecWalker`** — walks a spec document to produce an operation tree and extract schemas
- **`OperationFilter`** — glob-based filtering for paths/channels (include/exclude patterns)
- **`ExtractedSchema`** — a schema reference with its role and originating operation
- **`OperationNode`** / **`OperationInfo`** — the operation tree structure
- **`SchemaRole`** — identifies how a schema is used (request body, response, parameter, message payload, etc.)

## Usage

```csharp
using Corvus.Text.Json.OpenApi;

// Create a walker for your spec version
ISpecWalker walker = new OpenApi31Walker();

// Get the operation tree (optionally filtered)
byte[] specBytes = File.ReadAllBytes("petstore.json");
var filter = new OperationFilter(includePaths: ["/pets/**"]);
OperationNode root = walker.GetOperationTree(specBytes, filter);

// Extract schemas for code generation
foreach (ExtractedSchema schema in walker.ExtractSchemas(specBytes, filter))
{
    Console.WriteLine($"{schema.SchemaReference} ({schema.Role}) from {schema.OperationId}");
}
```

## Related Packages

- `Corvus.Text.Json.OpenApi30` — V5 types for OpenAPI 3.0 specs
- `Corvus.Text.Json.OpenApi31` — V5 types for OpenAPI 3.1 specs
- `Corvus.Text.Json` — Core library
