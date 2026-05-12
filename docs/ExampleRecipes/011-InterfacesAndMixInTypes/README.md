# JSON Schema Patterns in .NET - Interfaces and Mix-In Types

This recipe demonstrates how to compose multiple JSON Schema definitions using `allOf`, creating types that behave like .NET interfaces or mix-ins.

## The Pattern

.NET does not support the concept of multiple-inheritance or [mix-ins](https://en.wikipedia.org/wiki/Mixin).

However, you can implement multiple interfaces on a type.

While interfaces don't (generally) provide implementation ([though that has changed!](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interface-implementation/default-interface-methods-versions)), they do provide structure and semantic intent. This gives us the ability to define functions that operate on a particular interface, without having to know the details of the specific instance.

The equivalent in JSON Schema is to compose multiple schemas using the `allOf` keyword.

`allOf` lets us provide an array of schemas. As the name implies, *all* of the schema constraints are applied: both our local constraints, and those in each of the schemas in the `allOf` array.

You have to take care to ensure that they are mutually compatible, or you can get unexpected validation failures. (We can't be `allOf` a `{"type": "string"}` and a `{"type": "object"}`!).

## The Schemas

### composite-type.json

```json
{
    "title": "A composition of multiple different schema",
    "type": "object",
    "allOf": [
        { "$ref": "./countable.json" },
        { "$ref": "./documentation.json" }
    ],
    "required": [ "budget" ],
    "properties": {
        "budget": { "$ref": "#/$defs/currencyValue" }
    },
    "$defs": {
        "currencyValue": {
            "type": "number",
            "format": "decimal"
        }
    }
}
```

### documentation.json

```json
{
    "type": "object",
    "required": [ "title" ],
    "properties": {
        "description": { "type": "string" },
        "title": { "type": "string" }
    }
}
```

### countable.json

```json
{
    "type": "object",
    "required": [ "count" ],
    "properties": {
        "count": { "$ref": "#/$defs/positiveInt32" }
    },
    "$defs": {
        "positiveInt32": {
            "type": "integer",
            "format": "int32"
        }
    }
}
```

In this example, the composed schema conforms to both the `countable` and `documentation` schemas. These are represented as externally provided schema documents. As always, if they are under your control, you might choose to embed them locally in a single document.

The composite type adds its own additional constraint - a required property called `budget`.

## Generated Code

The code generator produces three types:
- **`Documentation`** - has `title` (required) and `description` (optional) properties
- **`Countable`** - has `count` (required) property
- **`CompositeType`** - combines both via `allOf` and adds `budget` (required)

Note that `CurrencyValue` is *not* generated as a separate type — the code generator detects that the `currencyValue` definition in `$defs` reduces to a built-in type (`JsonDecimal`), so it avoids generating unnecessary code. The `Budget` property on `CompositeType` uses `JsonDecimal` directly.

## Usage Examples

### Parsing a composite type

```csharp
string compositeJson = """
    {
      "budget": 123.7,
      "count": 4,
      "title": "Greeting",
      "description": "Hello world"
    }
    """;

using var parsedComposite = ParsedJsonDocument<CompositeType>.Parse(compositeJson);
CompositeType composite = parsedComposite.RootElement;

Console.WriteLine(composite);
```

### Converting to composed types

Like implementing multiple interfaces, you can implicitly convert the composite type to any of its `allOf` constituents:

```csharp
// Implicit conversion to the composed types
Documentation documentation = composite;
Countable countable = composite;

// Access properties from each "interface"
Console.WriteLine($"Title: {documentation.Title}");

// Description is an optional property - check IsUndefined() before use
if (!documentation.Description.IsUndefined())
{
    Console.WriteLine($"Description: {documentation.Description}");
}

Console.WriteLine($"Count: {countable.Count}");
Console.WriteLine($"Budget: {composite.Budget}");
```

The generated types handle optional properties through entity types that can be checked with `IsUndefined()`. This avoids the need for dictionary-style property access.

### Building a composite from its parts with `Apply()`

When you have instances of the constituent types from different sources, you can compose them into a composite using `Apply()` on the mutable builder. This merges the properties from each constituent into the composite:

```csharp
// Suppose we have Countable and Documentation instances from different sources
using var parsedCountable = ParsedJsonDocument<Countable>.Parse("""{"count": 10}""");
using var parsedDocumentation = ParsedJsonDocument<Documentation>.Parse(
    """{"title": "Composed", "description": "Built from parts"}""");

using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = CompositeType.CreateBuilder(workspace);
CompositeType.Mutable mutableComposite = builder.RootElement;

// Apply each constituent - merges its properties into the composite
mutableComposite.Apply(parsedCountable.RootElement);
mutableComposite.Apply(parsedDocumentation.RootElement);

// Set the composite's own property
mutableComposite.SetBudget(456.78m);

Console.WriteLine(mutableComposite);
// Output: {"count":10,"title":"Composed","description":"Built from parts","budget":456.78}
```

`Apply()` is generated for each `allOf` constituent type. It enumerates the properties of the provided value and adds or updates them in the mutable composite. This lets you assemble a composite from independently-sourced parts without having to extract and re-set each property individually.

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create directly
CompositeType composite = CompositeType.Create(123.7m, 4, "Greeting", "Hello world");

// Implicit conversion to composed types
Documentation documentation = composite;
Countable countable = composite;

// Check optional properties
if (documentation.Description.IsNotUndefined())
{
    Console.WriteLine($"Description: {documentation.Description}");
}
```

### V5 (Corvus.Text.Json)
```csharp
// Create using the convenience overload with named parameters
using JsonWorkspace workspace = JsonWorkspace.Create();
using var compositeBuilder = CompositeType.CreateBuilder(
    workspace,
    budget: 123.7m,
    count: 4,
    title: "Greeting",
    description: "Hello world");
CompositeType composite = compositeBuilder.RootElement;

// Implicit conversion to composed types (same as V4)
Documentation documentation = composite;
Countable countable = composite;

// Check optional properties with generated property (not TryGetProperty)
if (!documentation.Description.IsUndefined())
{
    Console.WriteLine($"Description: {documentation.Description}");
}
```

**Key differences:**
- V5 uses `CreateBuilder(workspace, prop: value, ...)` convenience method — similar ergonomics to V4's `Create()`
- V5 requires a `JsonWorkspace` for mutable operations
- V5 uses `IsUndefined()` instead of V4's `IsNotUndefined()`
- Implicit conversion to `allOf` constituents works the same in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/011-InterfacesAndMixInTypes
dotnet run
```

## Related Patterns

- [003-ReusingCommonTypes](../003-ReusingCommonTypes/) - Using `$ref` and `$defs` for shared types
- [005-ExtendingABaseType](../005-ExtendingABaseType/) - Inheritance via `allOf` with a single base
- [012-PatternMatching](../012-PatternMatching/) - Discriminated unions with `oneOf`

## Frequently Asked Questions

### Q: Is `allOf` composition the same as multiple inheritance?

**A:** Not exactly. `allOf` is structural composition — the generated type must satisfy all of the composed schemas simultaneously, similar to implementing multiple interfaces in C#. Unlike class inheritance, there is no method resolution order or diamond problem. Each constituent schema contributes its properties and constraints, and the generated type provides implicit conversions to access each "facet."

### Q: What happens if two composed schemas define the same property?

**A:** Both constraints apply. If two `allOf` constituents define a property with the same name, the value must satisfy both schemas simultaneously during validation. In the generated code, each constituent type provides its own accessor for the property, but they operate on the same underlying JSON data.

### Q: Can I add constraints when composing schemas?

**A:** Yes. You can add additional properties and constraints alongside the `allOf` keyword in the composite schema. These extra constraints apply on top of whatever the composed schemas require. This is how you create a type that combines existing schemas while adding its own unique requirements.

### Q: How does `allOf` differ from `$ref` for type extension?

**A:** A `$ref` imports a single schema definition, effectively creating a type alias or base type reference. `allOf` combines multiple schemas together, requiring the value to satisfy all of them. Use `$ref` when you want to extend one base type with additional properties (see [Recipe 005](../005-ExtendingABaseType/)), and `allOf` when you want to mix in capabilities from multiple independent schemas.
