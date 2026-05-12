# JSON Schema Patterns in .NET - String Enumerations and Pattern Matching

This recipe demonstrates how to use JSON Schema `enum` keyword with string values to create type-safe enumerations with pattern matching support.

## The Pattern

String enumerations are one of the most common patterns in API design. They provide:
- **Type safety** - only valid values are accepted
- **Self-documentation** - the allowed values are clearly defined in the schema
- **Pattern matching** - exhaustive handling of all possible values

JSON Schema's `enum` keyword defines a fixed set of allowed values.

## The Schema

File: `color.json`

```json
{
  "enum": ["red", "green", "blue"]
}
```

This simple schema constrains values to exactly one of the three strings: `"red"`, `"green"`, or `"blue"`.

## Generated Code Usage

### Constant values

The generated code creates an `EnumValues` class with pre-built constants for each enum value:

```csharp
Color red = Color.EnumValues.Red;
Color green = Color.EnumValues.Green;
Color blue = Color.EnumValues.Blue;
```

UTF-8 byte representations are also available:

```csharp
ReadOnlySpan<byte> redUtf8 = Color.EnumValues.RedUtf8;
```

### Extracting string values

```csharp
// Get the underlying string value
if (red.TryGetValue(out string? redStr))
{
    Console.WriteLine($"String value: {redStr}");
    // Output: String value: red
}
```

### Pattern matching over enum values

Use the `Match()` method for exhaustive pattern matching:

```csharp
string DescribeColor(in Color color)
{
    return color.Match(
        matchRed: static () => "The color of fire and passion",
        matchGreen: static () => "The color of nature and growth",
        matchBlue: static () => "The color of sky and ocean",
        defaultMatch: static () => throw new InvalidOperationException("Unknown color"));
}

Console.WriteLine(DescribeColor(red));    // Output: The color of fire and passion
Console.WriteLine(DescribeColor(green));  // Output: The color of nature and growth
Console.WriteLine(DescribeColor(blue));   // Output: The color of sky and ocean
```

The compiler ensures you handle all possible enum values. If you add a new value to the schema and regenerate the code, any incomplete pattern matching will be caught at compile time.

### Pattern matching with a context parameter

When you need to pass state into your match functions, use the context parameter overload. This lets you use `static` lambdas (avoiding closure allocations) while still threading external state through to each handler:

```csharp
string ConvertToRgb(in Color color, double brightness)
{
    return color.Match(
        brightness,  // context parameter passed to all match functions
        matchRed: static (ctx) => $"RGB({(int)(255 * ctx)}, 0, 0)",
        matchGreen: static (ctx) => $"RGB(0, {(int)(255 * ctx)}, 0)",
        matchBlue: static (ctx) => $"RGB(0, 0, {(int)(255 * ctx)})",
        defaultMatch: static (ctx) => "RGB(0, 0, 0)");
}

double brightnessFactor = 0.8;
Console.WriteLine(ConvertToRgb(red, brightnessFactor));
// Output: RGB(204, 0, 0)
```

> **Tip:** JSON Schema `enum` with all-string values is the simplest way to model string enumerations. For richer semantics (titles, descriptions per value), use the `oneOf`+`const` pattern shown in [Recipe 015](../015-NumericEnumerations/).

## Key Differences from V4

The `Match()` API for string enumerations is essentially the same between V4 and V5 — both use named parameters (`matchRed:`, `matchGreen:`, etc.) with exhaustive handling.

### V4 (Corvus.Json)
```csharp
// Constant values
Color red = Color.EnumValues.Red;

// Pattern matching
string desc = red.Match(
    matchRed: static () => "The color of fire",
    matchGreen: static () => "The color of grass",
    matchBlue: static () => "The color of sky",
    defaultMatch: static () => "Unknown");
```

### V5 (Corvus.Text.Json)
```csharp
// Constant values (same pattern)
Color red = Color.EnumValues.Red;

// Pattern matching (same pattern)
string desc = red.Match(
    matchRed: static () => "The color of fire",
    matchGreen: static () => "The color of grass",
    matchBlue: static () => "The color of sky",
    defaultMatch: static () => "Unknown");
```

**Key differences:**
- The `Match()` API (including the context parameter overload), `EnumValues` constants, and exhaustive handling work the same way in both versions
- V5 uses `ParsedJsonDocument<T>` for parsing from external JSON input

## Running the Example

```bash
cd docs/ExampleRecipes/014-StringEnumerations
dotnet run
```

## Related Patterns

- [012-PatternMatching](../012-PatternMatching/) - Discriminated unions with `oneOf`
- [013-PolymorphismWithDiscriminators](../013-PolymorphismWithDiscriminators/) - Using `const` for discrimination
- [015-NumericEnumerations](../015-NumericEnumerations/) - Enumerations with numeric values and documentation

## Frequently Asked Questions

### Q: What happens if I parse a value not in the enum?

**A:** Parsing succeeds — the value is still a valid JSON string. However, schema validation via `EvaluateSchema()` will report it as invalid. The `Match()` method will route non-matching values to the `defaultMatch` handler. This design lets you handle unexpected values gracefully rather than throwing exceptions during parsing.

### Q: Can I use `enum` with mixed types (strings and numbers)?

**A:** JSON Schema's `enum` keyword accepts an array of any JSON values, so mixed types are technically valid. However, homogeneous enum arrays are easier to understand and work with. For heterogeneous enumerations, `oneOf` with `const` values is a better approach — it lets you document each variant individually and provides clearer discrimination (see [Recipe 012](../012-PatternMatching/)).

### Q: When should I use `enum` vs `oneOf` + `const`?

**A:** Use `enum` for simple value lists where you just need to constrain a property to a known set of values — it's more concise and produces cleaner schemas. Use `oneOf` + `const` when you need per-value documentation, named constant instances, or when your enum values are numeric (see [Recipe 015](../015-NumericEnumerations/)).

### Q: Is pattern matching over enums exhaustive at compile time?

**A:** The generated `Match()` method requires a handler for every enum value plus a `defaultMatch` fallback, so you won't miss a case at compile time. However, if you add new values to the schema and regenerate, existing code will get a compile error for the missing handler — which is exactly the safety net you want.
