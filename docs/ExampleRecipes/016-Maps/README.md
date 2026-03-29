# JSON Schema Patterns in .NET - Maps of Strings to Strongly Typed Values

This recipe demonstrates how to use JSON Schema `additionalProperties` to create strongly-typed map/dictionary structures with string keys and typed values.

## The Pattern

In .NET, we often use `IDictionary<string, T>` or `IReadOnlyDictionary<string, T>` to represent collections of key-value pairs where the keys are strings and the values are of a specific type.

JSON Schema provides the `additionalProperties` keyword (or `unevaluatedProperties` in draft 2020-12 with composition) to define the schema for values in such a map.

This is especially useful for:
- Configuration objects with arbitrary keys
- Flexible data structures where property names aren't known at design time
- API responses with dynamic property sets

## The Schema

File: `string-to-int-map.json`

```json
{
  "type": "object",
  "additionalProperties": {
    "type": "integer"
  }
}
```

This schema:
- Defines an object type
- Allows any property names (string keys)
- Constrains all property values to be integers

You could make this more complex by using a schema reference for the value type:

```json
{
  "type": "object",
  "additionalProperties": {
    "$ref": "./person.json"
  }
}
```

## Generated Code Usage

### Parsing a map

```csharp
string json = """
    {
      "foo": 1,
      "bar": 2,
      "baz": 3
    }
    """;

using var parsed = ParsedJsonDocument<StringToIntMap>.Parse(json);
StringToIntMap map = parsed.RootElement;
Console.WriteLine($"Map: {map}");
// Output: Map: {"foo":1,"bar":2,"baz":3}
```

### Accessing map values

Use `TryGetProperty()` with UTF-8 byte literals for zero-allocation property access:

```csharp
if (map.TryGetProperty("foo"u8, out var fooValue))
{
    Console.WriteLine($"foo = {fooValue}");
    // Output: foo = 1
}
```

You can also use the indexer, which returns an `Undefined` value rather than throwing when the key doesn't exist:

```csharp
var barValue = map["bar"u8];
Console.WriteLine($"bar = {barValue}");
// Output: bar = 2

var missing = map["noSuchKey"u8];
Console.WriteLine($"missing is undefined: {missing.IsUndefined()}");
// Output: missing is undefined: True
```

Both support UTF-8 byte literals (`"foo"u8`) for zero-allocation access, and string overloads for convenience.

### Enumerating all entries

```csharp
// Enumerate all key-value pairs
Console.WriteLine("All entries:");
foreach (var property in map.EnumerateObject())
{
    Console.WriteLine($"  {property.Name} = {property.Value}");
}
// Output:
// All entries:
// foo = 1
// bar = 2
// baz = 3
```

### Building a map (mutable)

While this simple example shows readonly access, you can create maps using the mutable builder pattern:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = StringToIntMap.CreateBuilder(workspace);
StringToIntMap.Mutable newMap = builder.RootElement;

// Add properties to the map
newMap.SetProperty("alpha"u8, 10);
newMap.SetProperty("beta"u8, 20);
newMap.SetProperty("gamma"u8, 30);

Console.WriteLine(newMap);
// Output: {"alpha":10,"beta":20,"gamma":30}
```

### Mutating an existing map

You can also create a mutable builder from an existing map to modify its properties:

```csharp
// Create a builder from the parsed map
using var mutatedBuilder = map.CreateBuilder(workspace);
StringToIntMap.Mutable mutableMap = mutatedBuilder.RootElement;

// Update an existing property
mutableMap.SetProperty("foo"u8, 100);

// Add a new property
mutableMap.SetProperty("qux"u8, 4);

// Remove a property
mutableMap.RemoveProperty("bar"u8);

Console.WriteLine(mutableMap);
// Output: {"foo":100,"baz":3,"qux":4}
```

The mutable map allows you to dynamically add, update, and remove properties with `SetProperty()` and `RemoveProperty()`, providing a flexible way to construct and modify maps at runtime.

## UTF-8 String Literals for Performance

Notice the use of `"foo"u8` syntax. The `u8` suffix creates a UTF-8 encoded `ReadOnlySpan<byte>` at compile time, avoiding:
- String allocation
- UTF-16 to UTF-8 transcoding at runtime

This is the preferred way to access properties in performance-sensitive code.

For compatibility or convenience, you can also use regular strings:

```csharp
string key = GetKeyFromSomewhere();
if (map.TryGetProperty(key, out var value))
{
    // ...
}
```

But be aware this will allocate and transcode the string.

## Key Differences from V4

Property access (`TryGetProperty()`, indexer), `EnumerateObject()`, and UTF-8 key overloads are available in both V4 and V5. The main difference is how maps are constructed.

### V4 (Corvus.Json)
```csharp
// Create from property collection
StringToIntMap map = StringToIntMap.FromProperties(
    ("foo", 1),
    ("bar", 2),
    ("baz", 3));
```

### V5 (Corvus.Text.Json)
```csharp
// Build with workspace
using JsonWorkspace workspace = JsonWorkspace.Create();
using var doc = StringToIntMap.CreateBuilder(workspace, 
    StringToIntMap.Build((ref StringToIntMap.Builder b) =>
    {
        b.SetProperty("foo"u8, 1);
        b.SetProperty("bar"u8, 2);
        b.SetProperty("baz"u8, 3);
    }));
```

**Key differences:**
- V5 uses the builder pattern instead of `FromProperties()` for constructing maps
- V5 uses `ParsedJsonDocument<T>` for parsing from external JSON input

## Running the Example

```bash
cd docs/ExampleRecipes/016-Maps
dotnet run
```

## Related Patterns

- [004-OpenVersusClosedTypes](../004-OpenVersusClosedTypes/) - Objects with `unevaluatedProperties: false`
- [011-InterfacesAndMixInTypes](../011-InterfacesAndMixInTypes/) - Composing object types
- [017-MappingInputAndOutputValues](../017-MappingInputAndOutputValues/) - Converting between different schemas

## Frequently Asked Questions

### Q: Can map values be complex types (objects, arrays)?

**A:** Yes. The `additionalProperties` keyword accepts any valid JSON Schema, so values can be objects, arrays, nested maps, or any other type. The generated code provides strongly-typed access to each value through `TryGetProperty()`, returning the value as the appropriate generated type.

### Q: How do I validate map keys against a pattern?

**A:** Use `patternProperties` instead of (or alongside) `additionalProperties`. With `patternProperties`, you define a regex pattern for the keys and a schema for the corresponding values. For example, `"^[a-z]+$": { "type": "integer" }` requires all matching keys to have integer values.

### Q: What's the difference between `additionalProperties` and `patternProperties`?

**A:** `additionalProperties` applies to all properties not explicitly defined by `properties` or matched by `patternProperties`. `patternProperties` applies only to keys matching specific regex patterns. You can combine both — use `patternProperties` for keys with known patterns and `additionalProperties` for everything else (or set it to `false` to disallow unmatched keys).

### Q: Can I use maps with known and unknown property combinations?

**A:** Yes. Define your known properties in the `properties` keyword and use `additionalProperties` to constrain the type of any extra properties. This gives you strongly-typed accessors for known properties while still allowing dynamic key-value entries. See [Recipe 004](../004-OpenVersusClosedTypes/) for more on open versus closed types.
