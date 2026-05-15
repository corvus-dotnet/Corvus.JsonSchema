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

For OpenAPI 3.1, use the self-contained `OpenApi31CodeGenerator` which walks the
strongly-typed model directly and emits C# source files:

```csharp
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi31;

// Collect schema pointers for the V5 code generator
string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(specRoot);

// Generate client code
var gen = new OpenApi31CodeGenerator("MyApp.Client", schemaTypeMap);
IReadOnlyList<GeneratedFile> files = gen.Generate(specRoot);
```

For OpenAPI 3.0, the `ISpecWalker` pipeline is used:

```csharp
using Corvus.Text.Json.OpenApi;

ISpecWalker walker = new OpenApi30Walker();
var filter = new OperationFilter(includePaths: ["/pets/**"]);

foreach (OperationEntry entry in walker.EnumerateOperations(specRoot, filter))
{
    Console.WriteLine($"{entry.OperationId} {entry.Method} {entry.PathTemplate}");
}
```

## Related Packages

- `Corvus.Text.Json.OpenApi30` — V5 types for OpenAPI 3.0 specs
- `Corvus.Text.Json.OpenApi31` — V5 types for OpenAPI 3.1 specs
- `Corvus.Text.Json` — Core library
