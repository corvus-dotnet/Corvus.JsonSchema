# JSON Schema Patterns in .NET - Mapping Input and Output Values

This recipe demonstrates how to efficiently convert between different schema representations of similar entities - a common pattern in layered architectures where data transforms between API, domain, and persistence layers.

## The Problem

A common problem in API-driven applications is the need to map data from one schema to another as information moves between the layers in your solution. The representation of an entity in your API is often similar, but not identical, to the representation of the same entity in your data store — or to the same entity in a third-party API on which you depend to provide your service.

In .NET applications it is common to use tools like [AutoMapper](https://docs.automapper.org/en/stable/) to help with this process. Corvus.Text.Json offers a schema-first alternative: because the generated types share a common set of property-entity interfaces, you can convert between arbitrary types using `From()` — even if they are compiled into different assemblies with no inheritance relationship of any kind between them. This works with zero (or near-zero) allocation, because `From()` creates a view over the same underlying JSON data rather than extracting to .NET primitives and re-serializing.

## The Pattern

In real-world applications, you often need to map data between different representations:
- **API schema** → **Domain model** → **Database schema**
- **External API response** → **Internal representation**
- **Legacy system format** → **Modern API format**

While these representations often contain similar data, property names and structures may differ. For example:
- API uses `id` and `name`
- Database uses `identifier` and `fullName`
- CRM uses `customerId` and `displayName`

In a real system you might have three or more representations of the same entity — one for your public API, one for a corporate CRM, and one for your database — each with its own naming conventions, additional constraints, or extra fields. The pattern shown here scales naturally to those multi-system scenarios.

Corvus.Text.Json provides efficient builder patterns for zero-allocation transformations between these formats.

## The Schemas

### source.json (Input format)

```json
{
  "type": "object",
  "required": ["id", "name"],
  "properties": {
    "id": { "type": "integer" },
    "name": { "type": "string" }
  }
}
```

### target.json (Output format)

```json
{
  "type": "object",
  "required": ["identifier", "fullName"],
  "properties": {
    "identifier": { "type": "integer" },
    "fullName": { "type": "string" }
  }
}
```

### crm.json (CRM format — constrained properties)

```json
{
  "type": "object",
  "required": ["customerId", "displayName"],
  "properties": {
    "customerId": {
      "type": "integer",
      "minimum": 1
    },
    "displayName": {
      "type": "string",
      "minLength": 1,
      "maxLength": 256
    }
  }
}
```

These are independent schemas that could be defined in different assemblies or namespaces. The generated types don't need to know about each other.

Notice that `source.json` and `target.json` have simple, unconstrained properties (`{ "type": "integer" }`, `{ "type": "string" }`), while `crm.json` adds validation constraints (`minimum`, `minLength`, `maxLength`). This distinction matters for how the code generator handles entity types — see [Using `From()` for constrained types](#level-1b-using-from-for-constrained-types) below.

## Generated Code Usage

### Parsing the source data

```csharp
string sourceJson = """
    {
      "id": 123,
      "name": "John Doe"
    }
    """;

using var parsedSource = ParsedJsonDocument<SourceType>.Parse(sourceJson);
SourceType source = parsedSource.RootElement;
Console.WriteLine($"Source - id: {source.Id}, name: {source.Name}");
// Output: Source - id: 123, name: John Doe
```

## Transformation Patterns

This recipe demonstrates four transformation patterns with increasing complexity:

### Level 1: Zero-Allocation Property Mapping

Use the `CreateBuilder()` convenience overload to map properties directly with named parameters:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var parsedSource = ParsedJsonDocument<SourceType>.Parse(sourceJson);
SourceType source = parsedSource.RootElement;

// Map to target type - property entities are compatible (zero-allocation view)
using var targetBuilder = TargetType.CreateBuilder(
    workspace,
    fullName: source.Name,
    identifier: source.Id);

TargetType target = targetBuilder.RootElement;
Console.WriteLine($"Target - identifier: {target.Identifier}, fullName: {target.FullName}");
// Output: Target - identifier: 123, fullName: John Doe
```

**Key points:**
- Property entity types are compatible when their underlying schemas match (both are strings, both are integers, etc.)
- When the types are reduced (e.g., a `{ "type": "string" }` property becomes `JsonString`), you can pass source properties directly as named parameters
- When types are not reduced (e.g., they have additional constraints), use `TargetEntityType.From(sourceProperty)` to convert between compatible entity types
- `From()` creates a zero-allocation view over the same underlying JSON data — no primitive extraction, no string copy, no re-serialization
- This works even if the source and target types are defined in different assemblies with no inheritance relationship between them; schema compatibility is what matters
- Only fall back to `TryGetValue()` when you actually need to transform the value (see Level 3 below)

### Level 1b: Using `From()` for Constrained Types

When the target schema adds constraints beyond the basic type (e.g., `minimum`, `minLength`, `maxLength`), the code generator produces distinct entity types instead of reducing them to `JsonInteger`/`JsonString`. In that case, you need `From()` to convert between the compatible but distinct entity types:

```csharp
// CRM properties have constraints, so entity types are NOT reduced.
// Use From() to convert between compatible entity types (still zero-allocation).
using var crmBuilder = CrmType.CreateBuilder(
    workspace,
    customerId: CrmType.CustomerIdEntity.From(source.Id),
    displayName: CrmType.DisplayNameEntity.From(source.Name));

CrmType crm = crmBuilder.RootElement;
Console.WriteLine($"CRM - customerId: {crm.CustomerId}, displayName: {crm.DisplayName}");
// Output: CRM - customerId: 123, displayName: John Doe
```

**Why is `From()` needed here?**
- `source.Id` is a `JsonInteger` (reduced — no constraints on the source schema)
- `CrmType.CustomerIdEntity` is a distinct type (not reduced — the schema specifies `minimum: 1`)
- There's no implicit conversion between these types, so you call `CrmType.CustomerIdEntity.From(source.Id)` to create a zero-allocation view
- `From()` does **not** validate the constraints — it creates a reinterpretation of the same underlying JSON. Validation happens separately via the schema validation API

### Level 2: Bidirectional Mapping

The same pattern works in reverse to map target back to source:

```csharp
// Reverse transformation (target -> source)
using var sourceBuilder = SourceType.CreateBuilder(
    workspace,
    id: target.Identifier,
    name: target.FullName);

SourceType reversedSource = sourceBuilder.RootElement;
Console.WriteLine(reversedSource);
// Output: {"id":123,"name":"John Doe"}
```

This demonstrates that compatibility works bidirectionally - you can map from source to target and back again using the same zero-allocation pattern.

### Level 3: Transformation with Value Modification

Only use `TryGetValue()` when you need to modify the actual values:

```csharp
// Extract ONLY when transforming values
if (source.Id.TryGetValue(out long idValue) && source.Name.TryGetValue(out string? nameValue) && nameValue is not null)
{
    using var modifiedBuilder = TargetType.CreateBuilder(
        workspace,
        fullName: nameValue.ToUpperInvariant(),
        identifier: idValue + 1000);

    TargetType modified = modifiedBuilder.RootElement;
    Console.WriteLine(modified);
    // Output: {"fullName":"JOHN DOE","identifier":1123}
}
```

**When to extract:**
- Use `TryGetValue()` only when performing arithmetic operations, string transformations, or other value modifications
- For simple remapping (like Levels 1 and 2), use `From()` to avoid allocation

### Multi-stage transformation pipelines

The `From()` and builder patterns chain naturally across multiple stages. Because all transformations share the same `JsonWorkspace`, you can build a pipeline — for example, API → Domain → Database — without extra allocation between stages. Each stage creates its builder from the previous stage's output, reusing the same workspace memory pool.

In a real system with three or more representations (see [the blog post](https://endjin.com/blog/json-schema-patterns-dotnet-mapping-input-and-output-values) for a worked example with API, CRM, and database customer schemas), you would apply the same `From()` pattern at each boundary, falling back to `TryGetValue()` only at stages that genuinely transform values.

## Performance Characteristics

The builder pattern provides several performance benefits:

1. **Workspace-scoped allocations** - All temporary buffers come from `ArrayPool<byte>` via the workspace
2. **Zero-copy transformations** - Property values are referenced, not copied, when possible
3. **Batch operations** - Multiple property sets happen in a single builder construction
4. **Deterministic cleanup** - `using` declarations ensure pooled memory is returned promptly

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create with builder method
TargetType target = TargetType.Create(
    identifier: source.Id,
    fullName: source.Name);

// Or use WithProperty for transformations
TargetType target = TargetType.Empty
    .WithProperty("identifier", source.Id)
    .WithProperty("fullName", source.Name);

// Or use As<T>() for compatible schemas
TargetType target = source.As<TargetType>();
```

### V5 (Corvus.Text.Json)
```csharp
// Use workspace and convenience overload with named parameters
using JsonWorkspace workspace = JsonWorkspace.Create();

// Map to target type - property entities are compatible (zero-allocation view)
using var targetBuilder = TargetType.CreateBuilder(
    workspace,
    fullName: source.Name,
    identifier: source.Id);

TargetType target = targetBuilder.RootElement;
```

**Key differences:**
- V5 requires explicit `JsonWorkspace` for memory management
- V5 uses `CreateBuilder()` with named parameters for zero-allocation mapping
- V5 makes allocation lifetime explicit through `using` declarations

## Running the Example

```bash
cd docs/ExampleRecipes/017-MappingInputAndOutputValues
dotnet run
```

## Related Patterns

- [001-DataObject](../001-DataObject/) - Creating and manipulating data objects
- [004-OpenVersusClosedTypes](../004-OpenVersusClosedTypes/) - Mutable operations on open types
- [016-Maps](../016-Maps/) - Working with dynamic property sets
- [011-InterfacesAndMixInTypes](../011-InterfacesAndMixInTypes/) - Type composition with `allOf`

## Real-World Scenarios

This pattern is especially useful for:

1. **API Gateways** - Transform external API responses to internal models
2. **Data Layer Mapping** - Convert domain models to database DTOs
3. **Event Transformation** - Reshape events between microservices
4. **Legacy Integration** - Adapt old formats to new schemas
5. **Multi-tenant Systems** - Transform data for different tenant schemas

## Frequently Asked Questions

### Q: Does `From()` copy the underlying JSON data?

**A:** No. `From()` performs a zero-copy type conversion — it reinterprets the same underlying JSON buffer as the target type. No data is duplicated or re-serialized. This is what makes `From()` so efficient for mapping between types that share structural compatibility. Actual data copying only happens when you mutate values through a `JsonDocumentBuilder`.

### Q: Can I map between types in different assemblies?

**A:** Yes, as long as both types are generated from schemas with compatible structures. The `From()` method works on the underlying JSON representation, not on .NET type identity. You can reference generated types from separate projects and convert between them using the same `From()` pattern.

### Q: When should I use `From()` vs `TryGetValue()`?

**A:** Use `From()` when you're converting an entire entity to a different schema representation without modifying values — it's zero-allocation and zero-copy. Use `TryGetValue()` when you need to extract a primitive value for transformation (e.g., converting a string to uppercase or reformatting a date) before building the output.

### Q: How does this compare to AutoMapper?

**A:** Unlike AutoMapper, which uses reflection and creates intermediate objects, the `From()` pattern operates directly on the JSON buffer with no reflection, no intermediate allocations, and no runtime configuration. The mapping is determined at compile time by the schema structure. This makes it significantly faster but limited to structural conversions — complex business logic transformations still require explicit code.
