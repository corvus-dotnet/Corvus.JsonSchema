# Corvus.Text.Json.OpenApi.CodeGeneration

Code generation for .NET API clients and servers from OpenAPI and AsyncAPI specifications.

Produces strongly-typed C# using Corvus.Text.Json zero-allocation types. The generated client
code parses API responses directly into document-backed value types — no intermediate object
model, no deserialization, no allocation on the happy path.

## Architecture

```
OpenAPI/AsyncAPI spec
  → Parse via V5 type libraries
  → Walk via ISpecWalker (zero-allocation)
  → Build ClientModel (operations, parameters, schemas)
  → Feed schemas to existing V5 JsonSchemaTypeBuilder
  → Emit client interfaces + implementations
  → Write .cs files
```

## Key Types

- `ClientModel` — complete API model: metadata, operations, schemas
- `ClientOperation` — one API method: path, HTTP method, parameters, request/response
- `ClientModelBuilder` — builds `ClientModel` from `ISpecWalker` output
- `ClientCodeEmitter` — produces C# source files from `ClientModel`