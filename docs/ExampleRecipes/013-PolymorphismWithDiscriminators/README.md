# JSON Schema Patterns in .NET - Polymorphism with Discriminator Properties

This recipe demonstrates how to use JSON Schema `oneOf` with `const` properties to create polymorphic types with discriminators - a pattern similar to OpenAPI's polymorphism feature and `System.Text.Json`'s polymorphic serialization.

## The Pattern

In the [previous recipe (012-PatternMatching)](../012-PatternMatching/), we looked at *untagged* discriminated unions — types discriminated by their fundamental structure (string vs. integer vs. object vs. array). That works well when the variants differ in JSON type, but most real-world APIs use *tagged* unions where all variants are objects and a specific property value identifies which variant you have. This recipe shows that more common pattern.

Here, each variant in a `oneOf` extends a common base and constrains a shared property (the discriminator) to a unique `const` value. This is the approach used by:
- [JSON Patch (RFC 6902)](https://jsonpatch.com/) — each patch operation (`add`, `remove`, `replace`, `move`, `copy`, `test`) has an `op` property constrained to a unique `const` value
- [OpenAPI's discriminator](https://swagger.io/docs/specification/data-models/inheritance-and-polymorphism/)
- [System.Text.Json's polymorphic serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism)
- Many API design patterns where a `type` field indicates which variant you're dealing with

## The Schema

File: `shape.json`

```json
{
  "oneOf": [
    {
      "type": "object",
      "required": ["type", "radius"],
      "properties": {
        "type": { "const": "circle" },
        "radius": { "type": "number" }
      }
    },
    {
      "type": "object",
      "required": ["type", "width", "height"],
      "properties": {
        "type": { "const": "rectangle" },
        "width": { "type": "number" },
        "height": { "type": "number" }
      }
    }
  ]
}
```

Each variant in the `oneOf`:
- Is an object type
- Has a `type` property with a `const` value unique to that variant
- Has additional properties specific to that variant

### Why `const` is essential

The `const` constraint on the discriminator property is what makes this pattern work with `oneOf`. Remember that `oneOf` requires *exactly one* of its sub-schemas to match a given input — if two or more match, validation fails.

Without `const`, two variants that share the same structure would both match the same input. Consider the JSON Patch specification: `MoveOperation` and `CopyOperation` are structurally identical — both extend a common base and add a `from` property with the same schema. Without `const` constraining the `op` property to `"move"` and `"copy"` respectively, any document valid against one would also be valid against the other, and `oneOf` would *always* be invalid (because two schemas match).

By giving each variant a unique `const` value on the discriminator, we guarantee that at most one schema in the `oneOf` array can match any given input, making the union well-discriminated.

## Generated Code Usage

### Parsing polymorphic types

```csharp
// Parse a circle
string circleJson = """
    {
      "type": "circle",
      "radius": 5.0
    }
    """;

using var parsedCircle = ParsedJsonDocument<Shape>.Parse(circleJson);
Shape circle = parsedCircle.RootElement;
Console.WriteLine($"Circle: {circle}");
// Output: Circle: {"type":"circle","radius":5}
```

### Accessing discriminated variant properties

Once you have a `Shape`, you can access its properties using the pattern matching API shown below, or by converting to the specific variant type:

```csharp
// Alternative: Direct variant access (not shown in Program.cs)
// This requires knowing the discriminator value upfront
Shape.RequiredRadiusAndType circleEntity = Shape.RequiredRadiusAndType.From(circle);
Console.WriteLine($"Circle radius: {circleEntity.Radius}");
```

**Note:** The primary recommended pattern is to use `Match()` (demonstrated below) as it provides exhaustive pattern matching and handles all variants safely. Direct variant access with `RequiredRadiusAndType.From()` can be used when you already know which variant you have, but requires manual validation of the discriminator.

### Pattern matching with discriminators

The `Match()` method provides type-safe exhaustive pattern matching using named parameters based on the required properties of each variant:

```csharp
string DescribeShape(in Shape shape)
{
    return shape.Match(
        matchRequiredRadiusAndType: static (in Shape.RequiredRadiusAndType circle) => 
            $"A circle with radius {circle.Radius}",
        matchRequiredHeightAndTypeAndWidth: static (in Shape.RequiredHeightAndTypeAndWidth rectangle) => 
            $"A rectangle {rectangle.Width}x{rectangle.Height}",
        defaultMatch: static (in Shape unknownShape) => 
            throw new InvalidOperationException($"Unknown shape: {unknownShape}"));
}
```

Note that the match handlers use named parameters (`matchRequiredRadiusAndType`, `matchRequiredHeightAndTypeAndWidth`) generated from the required properties in each discriminated variant.

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create instances directly
Circle circle = Circle.Create(radius: 5.0);
Rectangle rectangle = Rectangle.Create(width: 10.0, height: 20.0);

// Implicit conversion to discriminated union
Shape shapeFromCircle = circle;
Shape shapeFromRectangle = rectangle;

// Pattern matching with implicit type detection
string desc = shape.Match(
    (in Circle c) => $"Circle with radius {c.Radius}",
    (in Rectangle r) => $"Rectangle {r.Width}x{r.Height}");
```

### V5 (Corvus.Text.Json)
```csharp
// Parse from JSON
using var parsedCircle = ParsedJsonDocument<Shape>.Parse(circleJson);
Shape circle = parsedCircle.RootElement;

// Explicit conversion to variant entity
Shape.RequiredRadiusAndType circleEntity = Shape.RequiredRadiusAndType.From(circle);
double radius = circleEntity.Radius;

// Pattern matching with entity types (named by required properties)
string desc = shape.Match(
    matchRequiredRadiusAndType: (in Shape.RequiredRadiusAndType c) => $"Circle with radius {c.Radius}",
    matchRequiredHeightAndTypeAndWidth: (in Shape.RequiredHeightAndTypeAndWidth r) => $"Rectangle {r.Width}x{r.Height}",
    defaultMatch: (in Shape unknown) => throw new InvalidOperationException());
```

**Key differences:**
- V5 parses from JSON rather than providing `Create()` methods for discriminated types
- V5 names variant types by their required properties (e.g., `RequiredRadiusAndType`, `RequiredHeightAndTypeAndWidth`) instead of using ordinal `OneOfNEntity` names
- V5 requires explicit `From()` conversion to access variant-specific properties
- V5 pattern matching uses named parameters based on the variant type names

## Running the Example

```bash
cd docs/ExampleRecipes/013-PolymorphismWithDiscriminators
dotnet run
```

## Related Patterns

- [012-PatternMatching](../012-PatternMatching/) - Discriminated unions with heterogeneous types
- [014-StringEnumerations](../014-StringEnumerations/) - Enumerations and pattern matching
- [015-NumericEnumerations](../015-NumericEnumerations/) - Documented numeric enums with `const`

## Frequently Asked Questions

### Q: Why are variant types named by required properties (e.g. `RequiredRadiusAndType`)?

**A:** The code generator applies a `RequiredPropertyNameHeuristic` that builds a name from up to three `required` properties: `Required[Prop1]And[Prop2]And[Prop3]`. For example, a subschema with `"required": ["radius", "type"]` becomes `RequiredRadiusAndType`. If the subschema has more than three required properties, or the name collides with the parent type, the heuristic falls back to ordinal names like `OneOf0Entity`.

You can disable this behaviour using the `disabledNamingHeuristics` configuration option (e.g. `["RequiredPropertyNameHeuristic"]`), or set `useOptionalNameHeuristics: false` to disable all optional naming heuristics. You can also override individual type names using the CLI generator's `namedTypes` configuration or by adding an explicit model type:

```csharp
[JsonSchemaTypeGenerator("my-schema.json#/oneOf/0")]
public readonly partial struct Circle;
```

### Q: What if I forget the `const` on the discriminator property?

**A:** Without a `const` value, the discriminator property doesn't uniquely identify the variant, and more than one `oneOf` subschema could match the same input. This causes `oneOf` validation to fail because it requires exactly one match. Always pair your discriminator property with a `const` to ensure unambiguous variant selection.

### Q: Can I use a non-string discriminator?

**A:** Yes. The `const` keyword works with any JSON type — strings, numbers, booleans, or even `null`. However, string discriminators are the most common convention and align with OpenAPI's `discriminator` object. Non-string discriminators work identically in the generated code; the variant is still identified by matching the `const` value.

### Q: How does this relate to OpenAPI's discriminator?

**A:** This pattern is the JSON Schema equivalent of OpenAPI's `discriminator` object. Both use a property with a fixed value to identify which variant applies. The key difference is that JSON Schema uses `oneOf` + `const` for structural validation, while OpenAPI's discriminator is a hint for code generators. The Corvus.Text.Json generator recognizes the `oneOf` + `const` pattern and produces idiomatic match methods.
