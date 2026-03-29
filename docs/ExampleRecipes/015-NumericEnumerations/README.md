# JSON Schema Patterns in .NET - Numeric Enumerations

This recipe demonstrates how to use JSON Schema `enum` keyword with numeric values to create type-safe numeric enumerations.

## The Pattern

While string enumerations are common, sometimes you need numeric enumerations - perhaps for:
- Integration with existing systems that use numeric codes
- Compact wire format
- Bitwise operations

JSON Schema supports numeric `enum` values just like string values.

Using `oneOf` with `const` to create documented numeric enumerations provides several advantages over a simple `enum` array (`{"enum": [1, 2, 3]}`), which loses documentation context. The `oneOf` + `const` pattern provides:
- Named definitions for each value
- Title and description for each option
- Better IDE support and documentation generation

## The Schema

File: `status.json`

```json
{
  "oneOf": [
    { "$ref": "#/$defs/Pending" },
    { "$ref": "#/$defs/Active" },
    { "$ref": "#/$defs/Complete" }
  ],

  "$defs": {
    "Pending": {
      "title": "Pending status",
      "description": "The operation is waiting to start",
      "const": 1
    },
    "Active": {
      "title": "Active status",  
      "description": "The operation is currently running",
      "const": 2
    },
    "Complete": {
      "title": "Complete status",
      "description": "The operation has finished",
      "const": 3
    }
  }
}
```

This creates a numeric enumeration with three documented values. Each `const` defines a specific numeric value (1, 2, or 3), and the `title` and `description` provide context that will appear in generated documentation.

## Generated Code Usage

### Using static const instances

The generator creates a static const instance for each `oneOf` variant:

```csharp
// Use predefined const instances - zero allocation
Status pending = Status.Pending.ConstInstance;      // value: 1
Status active = Status.Active.ConstInstance;        // value: 2
Status complete = Status.Complete.ConstInstance;    // value: 3

Console.WriteLine($"Active status: {active}");
// Output: Active status: 2
```

**Benefits of const instances:**
- Zero allocation - reuses the same immutable instance
- Type-safe - compile-time correctness prevents invalid values
- Self-documenting - named instances (Pending, Active, Complete) instead of magic numbers
- Performance - no parsing overhead

### Parsing numeric enumeration values

You can also parse from JSON:

```csharp
string json = "1";
using var parsed = ParsedJsonDocument<Status>.Parse(json);
Status status = parsed.RootElement;
Console.WriteLine($"Status: {status}");
// Output: Status: 1
```

### Extracting numeric values

```csharp
// Get the underlying numeric value
if (status.TryGetValue(out int value))
{
    Console.WriteLine($"Numeric value: {value}");
    // Output: Numeric value: 1
}
```

### Validating enumeration values

You can use `EvaluateSchema()` to check whether a parsed value matches one of the defined enum constants:

```csharp
using var invalidDoc = ParsedJsonDocument<Status>.Parse("99");
Status invalid = invalidDoc.RootElement;
Console.WriteLine($"Status {invalid} is valid: {invalid.EvaluateSchema()}");
// Output: Status 99 is valid: False
```

### Pattern matching with documented variants

With the `oneOf` + `const` pattern, you get named pattern matching based on the variant types:

```csharp
string DescribeStatus(in Status status)
{
    return status.Match(
        matchPending: static (in Status.Pending _) => "Pending - waiting to start",
        matchActive: static (in Status.Active _) => "Active - currently running",
        matchComplete: static (in Status.Complete _) => "Complete - finished",
        defaultMatch: static (in Status _) => "Unknown status");
}
```

The match parameters use the names from your `$defs` (Pending, Active, Complete), making the code self-documenting. Each variant is a distinct type, providing full type safety.

### Pattern matching with context

You can pass additional state through the match:

```csharp
string ProcessStatus(in Status status, int requestCount)
{
    return status.Match(
        requestCount,  // context parameter
        matchPending: static (in Status.Pending _, in int count) => 
            $"Queued {count} requests - system pending",
        matchActive: static (in Status.Active _, in int count) => 
            $"Processing {count} requests on active system",
        matchComplete: static (in Status.Complete _, in int count) => 
            $"Cannot process {count} requests - system complete",
        defaultMatch: static (in Status _, in int count) => 
            throw new InvalidOperationException($"Unknown status cannot process {count} requests"));
}
```

## Why Use oneOf + const Instead of enum?

The simple `enum` approach (`{"enum": [1, 2, 3]}`) works but has limitations:

```json
{
    "enum": [1, 2, 3]
}
```

**Limitations:**
- No documentation for each value
- Generic generated names (EnumValues.NumberOne, NumberTwo, NumberThree)
- No semantic meaning attached to the numbers

The `oneOf` + `const` pattern shown in this recipe provides:
- Meaningful names for each numeric value (`Pending`, `Active`, `Complete`)
- Rich documentation via `title` and `description`
- Separate types for each value (Status.Pending, Status.Active, Status.Complete)
- Type-safe pattern matching with named parameters
- Prevents implicit numeric conversions, improving type safety

For more details on this pattern, see the [blog post](https://endjin.com/blog/2024/05/json-schema-patterns-dotnet-numeric-enumerations-and-pattern-matching).

## Key Differences from V4

The `oneOf` + `const` pattern and its `Match()` API are essentially the same between V4 and V5. Both generate variant types with `ConstInstance` properties and exhaustive pattern matching.

### V4 (Corvus.Json)
```csharp
// Access constant instances
Status pending = Status.Pending.ConstInstance;

// Pattern matching
string desc = status.Match(
    matchPending: static (in Status.Pending _) => "Pending",
    matchActive: static (in Status.Active _) => "Active",
    matchComplete: static (in Status.Complete _) => "Complete",
    defaultMatch: static (in Status _) => "Unknown");
```

### V5 (Corvus.Text.Json)
```csharp
// Access constant instances (same pattern)
Status pending = Status.Pending.ConstInstance;

// Pattern matching (same pattern)
string desc = status.Match(
    matchPending: static (in Status.Pending _) => "Pending",
    matchActive: static (in Status.Active _) => "Active",
    matchComplete: static (in Status.Complete _) => "Complete",
    defaultMatch: static (in Status _) => "Unknown");
```

**Key differences:**
- The `Match()` API (including the context parameter overload), `ConstInstance` properties, and exhaustive handling work the same way in both versions
- V5 uses `ParsedJsonDocument<T>` for parsing from external JSON input

## Running the Example

```bash
cd docs/ExampleRecipes/015-NumericEnumerations
dotnet run
```

## Related Patterns

- [014-StringEnumerations](../014-StringEnumerations/) - String-based enumerations
- [013-PolymorphismWithDiscriminators](../013-PolymorphismWithDiscriminators/) - Using `const` for discrimination
- [012-PatternMatching](../012-PatternMatching/) - Discriminated unions with `oneOf`

## Frequently Asked Questions

### Q: Why use `oneOf` + `const` instead of a simple `enum` array?

**A:** The `oneOf` + `const` pattern gives each numeric value a name, a `title`, and a `description` in the schema. This produces named constant instances (e.g., `Status.Pending.ConstInstance`) and descriptive match handler parameters in the generated code. A simple `enum` array like `[0, 1, 2]` provides no documentation or named access.

### Q: Can I combine numeric and string enumerations?

**A:** Each `oneOf` variant can use any `const` type, so you could technically mix them. In practice, it's better to keep enumerations homogeneous — use numeric values for status codes and bitfields, and string values for human-readable identifiers. Mixing types makes pattern matching and `TryGetValue()` extraction more complex.

### Q: How do I add a new enum value without breaking existing code?

**A:** Add a new entry to the `oneOf` array in your schema and regenerate the types. The generated `Match()` method will gain a new handler parameter, causing a compile error in every call site that hasn't been updated. This is intentional — it ensures you handle the new value everywhere rather than silently ignoring it.

### Q: Are `ConstInstance` values allocated on the heap?

**A:** No. `ConstInstance` properties return a struct-based JSON element backed by a static, pre-parsed byte buffer. There is no heap allocation when accessing them. This makes them ideal for comparisons and for constructing new instances from known values without any runtime parsing cost.
