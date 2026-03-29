# JSON Schema Patterns in .NET - Strongly Typed Arrays

This recipe demonstrates how to define strongly-typed JSON arrays in .NET using JSON Schema's `items`, `minItems`, and `maxItems` keywords, giving you compile-time type safety, IntelliSense support, and standard LINQ operators for array elements.

## The Pattern

JSON schema allows you define an `array` whose items are of a particular type using its `items` property.

This would be an array of rank 1, with an unbounded size - you could put no items in it `[]` or any number of items `[1, 2, 3, ...]`

If you wish to constrain it to a particular length, then you can specify either or both of `minItems` and `maxItems`.

- Specifying just `maxItems` indicates that the array can can contain *up to* that many items.
- Specifying just `minItems` indicates that the array will have *at least* that many items.
- Specifying both, and making them the same value implies an array of *exactly* that number of items.
- Specifying both, and making them different values gives an upper and a lower bound to the number of items.

In this case we are defining an array which must contain exactly 30 `PersonClosed` instances.

The code generator recognizes this array pattern and generates strongly typed accessors and enumerators. This gives you compile-time type safety when accessing array elements — no casting from a generic `JsonElement` — and full IntelliSense support for the item type's properties.

In addition, it implements `IEnumerable<T>` so that you can take advantage of standard LINQ operators if required.

## The Schemas

File: `person-1d-array.json`

```json
{
  "title": "A 1-dimensional array of Person instances",
  "type": "array",
  "items": { "$ref": "./person-closed.json" },
  "minItems": 30,
  "maxItems": 30
}
```

File: `person-closed.json`

```json
{
  "title": "The person schema https://schema.org/Person",
  "type": "object",
  "required": [ "familyName", "givenName", "birthDate" ],
  "properties": {
    "familyName": { "$ref": "#/$defs/constrainedString" },
    "givenName": { "$ref": "#/$defs/constrainedString" },
    "otherNames": { "$ref": "#/$defs/constrainedString" },
    "birthDate": {
      "type": "string",
      "format": "date"
    },
    "height": {
      "type": "number",
      "format": "double",
      "exclusiveMinimum": 0.0,
      "maximum": 3.0
    }
  },

  "unevaluatedProperties": false,

  "$defs": {
    "constrainedString": {
      "type": "string",
      "minLength": 1,
      "maxLength": 256
    }
  }
}
```

## Generated Code Usage

[Example code](./Program.cs)

### Parsing and accessing array elements

```csharp
using Corvus.Text.Json;
using CreatingAStronglyTypedArray.Models;
using NodaTime;

// Parse an array of 30 people from JSON
string peopleArrayJson =
    """
    [
      {"familyName":"Smith","givenName":"John","otherNames":"Edward,Michael","birthDate":"2004-01-01","height":1.8},
      {"familyName":"Johnson","givenName":"Alice","birthDate":"2000-02-02","height":1.6},
      {"familyName":"Williams","givenName":"Robert","otherNames":"James,Thomas","birthDate":"1995-03-03","height":1.7}
      // ... 27 more person objects (see Program.cs for full array)
    ]
    """;

using var parsedArray = ParsedJsonDocument<Person1dArray>.Parse(peopleArrayJson);
Person1dArray peopleArray = parsedArray.RootElement;

// Access strongly-typed values by array index
PersonClosed personAtIndex0 = peopleArray[0];
PersonClosed personAtIndex1 = peopleArray[1];

if (peopleArray.EvaluateSchema())
{
    // The array is valid - there are 30 valid PersonClosed instances in it.
    Console.WriteLine("original array is valid.");
}
```

### Mutable array operations

```csharp
// Mutable array operations via the builder pattern.
// The builder uses a JsonWorkspace to manage pooled memory, so a sequence of
// mutations operates in-place rather than allocating a new document for every
// change — much more memory-efficient than returning immutable copies.
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = peopleArray.CreateBuilder(workspace);
Person1dArray.Mutable root = mutableDoc.RootElement;

// Item removed at index
root.RemoveAt(0);
// Remove first instance of a specific value
root.Remove(personAtIndex1);

Person1dArray updatedArray = mutableDoc.RootElement;
if (updatedArray.EvaluateSchema())
{
    Console.WriteLine("Updated array is valid.");
}
else
{
    // The array is no longer valid - the length is 28
    Console.WriteLine("Updated array is not valid.");
}

// Insert an item at an index
root.InsertItem(0, personAtIndex1);
// Add an item at the end
root.AddItem(personAtIndex0);

// Add multiple items at once
root.AddRange(static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem(PersonClosed.Build(
        static (ref PersonClosed.Builder pb) => pb.Create(
            birthDate: new LocalDate(1930, 6, 12),
            familyName: "Doe"u8,
            givenName: "Jane"u8,
            otherNames: "Q."u8)));
    b.AddItem(PersonClosed.Build(
        static (ref PersonClosed.Builder pb) => pb.Create(
            birthDate: new LocalDate(1945, 3, 8),
            familyName: "Smith"u8,
            givenName: "Bob"u8,
            otherNames: "R."u8)));
});

// Insert multiple items at a specific index
root.InsertRange(2, static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem(PersonClosed.Build(
        static (ref PersonClosed.Builder pb) => pb.Create(
            birthDate: new LocalDate(1950, 9, 21),
            familyName: "Chen"u8,
            givenName: "Wei"u8,
            otherNames: "X."u8)));
});

updatedArray = mutableDoc.RootElement;
if (updatedArray.EvaluateSchema())
{
    // The array is valid - the length is back up to 30
    Console.WriteLine("Updated array is valid.");
}
```

### Setting and replacing items

```csharp
// Set item at a specific index
root.SetItem(14, PersonClosed.Build(
    static (ref PersonClosed.Builder b) => b.Create(
        birthDate: new LocalDate(1820, 1, 17),
        familyName: "Brontë",
        givenName: "Anne",
        height: 1.57)));

// Replace the first instance of a person by value
root.Replace(personAtIndex0, PersonClosed.Build(
    static (ref PersonClosed.Builder b) => b.Create(
        birthDate: new LocalDate(1820, 1, 17),
        familyName: "Brontë",
        givenName: "Anne",
        height: 1.57)));
```

### Enumerating items

```csharp
// Enumerate the items in the array
updatedArray = mutableDoc.RootElement;
foreach (PersonClosed enumeratedPerson in updatedArray.EnumerateArray())
{
    Console.WriteLine($"{enumeratedPerson.FamilyName}, {enumeratedPerson.GivenName}");
}
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Parse and access array elements
Person1dArray peopleArray = Person1dArray.Parse(peopleArrayJson);
PersonClosed person = peopleArray[0];

// Create an array from values using collection expressions
Person1dArray array = [person1, person2, person3];

// Immutable operations — each mutation returns a new copy
Person1dArray updated = peopleArray.RemoveAt(0);
updated = updated.InsertItem(0, newPerson);

// Enumerate
foreach (PersonClosed p in peopleArray.EnumerateArray())
{
    Console.WriteLine($"{p.FamilyName}, {p.GivenName}");
}
```

### V5 (Corvus.Text.Json)
```csharp
// Parse with explicit document lifetime
using var parsedArray = ParsedJsonDocument<Person1dArray>.Parse(peopleArrayJson);
Person1dArray peopleArray = parsedArray.RootElement;
PersonClosed person = peopleArray[0];

// Mutable operations via workspace builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = peopleArray.CreateBuilder(workspace);
Person1dArray.Mutable root = mutableDoc.RootElement;
root.RemoveAt(0);
root.InsertItem(0, newPerson);

// Enumerate (same API)
foreach (PersonClosed p in mutableDoc.RootElement.EnumerateArray())
{
    Console.WriteLine($"{p.FamilyName}, {p.GivenName}");
}
```

**Key differences:**
- V5 uses `ParsedJsonDocument<T>.Parse()` with `using` for explicit lifetime management
- V5 mutations are in-place via the builder pattern (`CreateBuilder(workspace)`) instead of returning new immutable copies
- V5 does not support collection expressions for constructing arrays — parse from JSON or use the builder
- V5 requires a `JsonWorkspace` for mutable operations, providing pooled memory management
- Enumeration and indexing APIs are the same in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/007-CreatingAStronglyTypedArray
dotnet run
```

## Related Patterns

- [008-CreatingAnArrayOfHigherRank](../008-CreatingAnArrayOfHigherRank/) - Multi-dimensional arrays
- [009-WorkingWithTensors](../009-WorkingWithTensors/) - Working with tensors and numeric spans
- [010-CreatingTuples](../010-CreatingTuples/) - Creating tuples with `prefixItems`

## Frequently Asked Questions

### What's the difference between an array and a tuple in JSON Schema?

An **array** (using `items`) defines a uniform collection where every element conforms to the same schema. A **tuple** (using `prefixItems`) defines a fixed-length, ordered collection where each position has its own schema. Use arrays when all elements are the same type; use tuples when you need heterogeneous, positional elements — see [Recipe 010](../010-CreatingTuples/).

### How do I enumerate array elements?

Call `EnumerateArray()` to get a strongly-typed enumerator over the array elements. This works on both immutable (`Person1dArray`) and mutable (`Person1dArray.Mutable`) views. The generated type also implements `IEnumerable<T>`, so you can use standard LINQ operators.

### When should I use the mutable builder versus working with immutable arrays?

Use the **immutable** parsed document when you only need to read and validate data — it's the most efficient path. Switch to the **mutable builder** (`CreateBuilder(workspace)`) when you need to add, remove, replace, or reorder elements. The builder operates in-place on pooled memory, avoiding the allocation overhead of creating a new document for each change.

### What do `minItems` and `maxItems` actually enforce?

These keywords constrain the array length at **validation** time, not at the type level. You can always mutate an array to have fewer or more items than the schema allows — `EvaluateSchema()` will simply return `false`. Setting both to the same value (e.g., 30) means the array must contain *exactly* that many items to be valid. The code generator also uses matching `minItems`/`maxItems` to recognize fixed-size arrays for tensor operations.