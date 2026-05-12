# JSON Schema Patterns in .NET - Creating Tuples

This recipe demonstrates how to use JSON Schema `prefixItems` and `unevaluatedItems` to create strongly-typed tuple representations in .NET.

## The Pattern

.NET has the concept of a [`ValueTuple<T1,...TN>`](https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple) - a lightweight type that can represent a small, strongly typed collection of values.

We typically encounter it through the special C# syntax available to define and instantiate them:

```csharp
(int, string, bool) value = (3, "hello", true);

Console.WriteLine($"{value.Item1}, {value.Item2}, {value.Item3}");
```

JSON Schema allows us to define an array whose items are constrained to a specific ordered list of schemas using the `prefixItems` keyword.

To ensure that no other items are permitted than those in the ordered list, we also add `unevaluatedItems: false`.

> **Note:** This is an area of divergence between draft 2020-12 and prior drafts. In those versions, you should use the array form of `items` and `additionalItems`.

Notice that a tuple is in effect a *closed type* - you cannot add additional items to it. Just as `unevaluatedProperties: false` closes an `object` type, `unevaluatedItems: false` closes an array type.

## The Schema

File: `three-tuple.json`

```json
{
    "title":  "A tuple of int32, string, boolean",
    "type": "array",
    "prefixItems": [
        {
            "type": "integer",
            "format": "int32"
        },
        {
            "type": "string"
        },
        {
            "type": "boolean"
        }
    ],
    "unevaluatedItems": false
}
```

## Generated Code Usage

The generated `ThreeTuple` type provides:
- **`Item1`, `Item2`, `Item3` properties** for accessing tuple elements by position
- **`CreateTuple(T1, T2, T3)` method** on the builder for creating tuples
- **Parse and equality support** like other generated types

### Parsing a tuple from JSON

```csharp
string threeTupleJson = """
    [3, "Hello", false]
    """;

using var parsedTuple = ParsedJsonDocument<ThreeTuple>.Parse(threeTupleJson);
ThreeTuple threeTuple = parsedTuple.RootElement;

Console.WriteLine(threeTuple);
// Output: [3,"Hello",false]
```

### Accessing tuple items

```csharp
Console.WriteLine($"Item1: {threeTuple.Item1}");  // Output: Item1: 3
Console.WriteLine($"Item2: {threeTuple.Item2}");  // Output: Item2: Hello
Console.WriteLine($"Item3: {threeTuple.Item3}");  // Output: Item3: False
```

### Creating a tuple with the builder

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builtDoc = ThreeTuple.CreateBuilder(workspace, ThreeTuple.Build(static (ref ThreeTuple.Builder b) =>
{
    b.CreateTuple(42, "World", true);
}));
ThreeTuple threeTuple2 = builtDoc.RootElement;

Console.WriteLine(threeTuple2);
// Output: [42,"World",true]
```

### Creating a tuple directly from positional sources

For **fixed-size tuples** (those defined with `unevaluatedItems: false` or `items: false`), you can use the `CreateBuilder` convenience overload that takes positional values directly, without the delegate indirection:

```csharp
using var directDoc = ThreeTuple.CreateBuilder(workspace, 99, "Direct", false);
ThreeTuple threeTuple3 = directDoc.RootElement;

Console.WriteLine(threeTuple3);
// Output: [99,"Direct",false]
```

Each parameter corresponds to a tuple item position, and accepts the `Source` type for that position (with implicit conversions from the underlying .NET type, e.g. `int`, `string`, `bool`).

If you prefer to separate construction from materialisation (e.g., to pass the source to another method), use `Build()` + `CreateBuilder()`:

```csharp
ThreeTuple.Source tupleSource = ThreeTuple.Build(99, "Direct", false);
using var directDoc = ThreeTuple.CreateBuilder(workspace, tupleSource);
```

> **Note:** These convenience overloads are only available on **pure tuple** types (closed with `unevaluatedItems: false` or `items: false`). Tuples that allow additional items must still use the `Build` delegate + `CreateTuple` pattern.

### Comparing tuples

```csharp
if (threeTuple.Equals(threeTuple2))
{
    Console.WriteLine("The tuples are equal");
}
else
{
    Console.WriteLine("The tuples are not equal");
}
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create from .NET tuple (implicit conversion)
(int, string, bool) dotnetTuple = (3, "Hello", false);
ThreeTuple threeTuple = dotnetTuple;

// Create directly
ThreeTuple threeTuple2 = ThreeTuple.Create(3, "Hello", false);

// Convert to .NET tuple (implicit conversion)
(int, JsonString, bool) dotnetTupleFromThreeTuple = threeTuple;
```

### V5 (Corvus.Text.Json)
```csharp
// Create directly (preferred for fixed-size tuples)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var doc = ThreeTuple.CreateBuilder(workspace, 42, "World", true);
ThreeTuple threeTuple = doc.RootElement;

// Or create via Build + CreateBuilder (two-step equivalent)
ThreeTuple.Source source = ThreeTuple.Build(42, "World", true);
using var doc2 = ThreeTuple.CreateBuilder(workspace, source);

// Or create via Build delegate + CreateTuple (required for tuples with additional items)
using var doc3 = ThreeTuple.CreateBuilder(workspace, ThreeTuple.Build(static (ref ThreeTuple.Builder b) =>
{
    b.CreateTuple(42, "World", true);
}));

// Access items (same as V4)
int item1 = threeTuple.Item1;
string item2 = threeTuple.Item2;  // Formatting support - no explicit cast needed
bool item3 = threeTuple.Item3;
```

**Important:** V5 does not provide implicit conversions to/from `ValueTuple`. This is by design - the generated types support direct formatting and property access, eliminating the need for tuple conversions in most scenarios.

## Running the Example

```bash
cd docs/ExampleRecipes/010-CreatingTuples
dotnet run
```

## Related Patterns

- [007-CreatingAStronglyTypedArray](../007-CreatingAStronglyTypedArray/) - Arrays with uniform item types
- [008-CreatingAnArrayOfHigherRank](../008-CreatingAnArrayOfHigherRank/) - Multi-dimensional arrays
- [011-InterfacesAndMixInTypes](../011-InterfacesAndMixInTypes/) - Composing types with `allOf`

## Frequently Asked Questions

### Q: What's the difference between a tuple and a fixed-size array?

**A:** A tuple uses `prefixItems` to define a specific type for each positional element (e.g., `[int, string, bool]`), while a fixed-size array uses `items` to constrain all elements to a single type. Tuples give you strongly-typed access to each item via `Item1`, `Item2`, etc., whereas arrays provide uniform element access through indexing.

### Q: Can I create open tuples that allow additional items?

**A:** Yes. By default, JSON Schema arrays allow additional items beyond those listed in `prefixItems`. To create a closed tuple that rejects extra items, add `"unevaluatedItems": false` to your schema. Without that constraint, additional items of any type are permitted after the defined prefix items.

### Q: Why doesn't V5 support implicit conversion to/from ValueTuple?

**A:** This is by design. V5 generated types support direct formatting and strongly-typed property access (`Item1`, `Item2`, etc.), which eliminates the need for `ValueTuple` conversions in most scenarios. Removing implicit conversions avoids hidden allocations and keeps the API surface explicit about when data copying occurs.

### Q: Can tuple items be complex types (objects, arrays)?

**A:** Absolutely. Each position in `prefixItems` can reference any valid JSON Schema, including objects with properties, nested arrays, `$ref` references to shared definitions, or even other tuples. The generated code will provide strongly-typed access to each item using the appropriate generated type.
