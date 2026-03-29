# JSON Schema Patterns in .NET - Simple Data Objects

This recipe demonstrates how to define a simple data object composed of primitive values using JSON Schema, and how the Corvus.Text.Json code generator produces strongly-typed .NET types with rich property accessors, zero-allocation comparisons, and NodaTime integration.

## The Pattern

It is very common to define a simple data object composed of primitive values for information exchange through an API.

An object is generated for the root schema, whose name is derived from the type declaration. It has .NET properties for each of the declared properties, with pascal-cased names. These can be used in much the same way as primitive .NET types like `string`, `double` and `NodaTime.LocalDate`.

(Note that we use [NodaTime](https://nodatime.org/) for our basic date/time types.)

Each generated property type is more than just a raw primitive wrapper. For example, `Person.FamilyNameEntity` wraps a JSON string value with type-safe operations: `ValueEquals()` for zero-allocation comparison against `string` or `ReadOnlySpan<byte>`, `TryGetValue()` for safe extraction, `IsUndefined()`/`IsNull()` for presence checks, format validation (e.g. `BirthDateEntity` validates the `date` format), and implicit/explicit conversions to .NET primitives. These entity types give you a rich, validated API over the underlying JSON data while keeping allocations to a minimum.

In the generated code, most of the "JSON-like" behaviours follow the patterns in `System.Text.Json` (e.g. parsing and writing JSON). But we try to make the accessors and usage patterns as .NET-like as possible, with implicit conversions to .NET primitives (where they do not allocate) so, by and large, everything "just works".

## The Schema

For example, here's a subset of the Person schema from [schema.org](https://schema.org/Person).

File: `person.json`

```json
{
    "title": "The person schema https://schema.org/Person",
    "type": "object",
    "properties": {
        "familyName": { "type": "string" },
        "givenName": { "type": "string" },
        "otherNames": { "type": "string" },
        "birthDate": { "type": "string", "format": "date" },
        "height": { "type": "number" }
    }
}
```

The generated .NET properties are:

- `FamilyName` — of type `Person.FamilyNameEntity` (a string type)
- `GivenName` — of type `Person.GivenNameEntity` (a string type)
- `OtherNames` — of type `Person.OtherNamesEntity` (a string type)
- `BirthDate` — of type `Person.BirthDateEntity` (a date-formatted string type, with implicit conversion to `NodaTime.LocalDate`)
- `Height` — of type `Person.HeightEntity` (a number type)

## Generated Code Usage

[Example code](./Program.cs)

### Creating a Person from .NET values

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var personDoc = Person.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.52);
```

### Serialization

```csharp
// Convert to JSON string (allocates)
string jsonString = personDoc.RootElement.ToString();
Console.WriteLine(jsonString);

// Write to a Utf8JsonWriter (does not allocate)
ArrayBufferWriter<byte> abw = new();
using (Utf8JsonWriter writer = new(abw))
{
    personDoc.RootElement.WriteTo(writer);
    writer.Flush();
}
```

### Parsing

```csharp
// From a string (produces an immutable document)
using var parsedDoc = ParsedJsonDocument<Person>.Parse(jsonString);
Person person = parsedDoc.RootElement;

// From UTF-8 bytes
using var parsedFromUtf8 = ParsedJsonDocument<Person>.Parse(abw.WrittenMemory);
```

### Property access

```csharp
// Explicit cast to string (throws if null or undefined)
string familyName = (string)person.FamilyName;
Console.WriteLine($"Family name: {familyName}");

// Try to get a string value which may not be present (does not throw)
if (person.GivenName.TryGetValue(out string? givenName))
{
    Console.WriteLine($"Given name: {givenName}");
}

// Check if an optional property is undefined
if (person.OtherNames.IsUndefined())
{
    Console.WriteLine("otherNames is not present.");
}
else
{
    Console.WriteLine("otherNames is present.");
}

// Implicit conversion to NodaTime LocalDate (does not allocate)
LocalDate date = person.BirthDate;
Console.WriteLine($"Birth date: {date}");

// Explicit conversion to double
double heightValue = (double)person.Height;
Console.WriteLine($"Height: {heightValue}");
```

### Mutation via builder

```csharp
// Create a modified copy via builder
// (start from an immutable parsed instance, then modify properties)
using var updatedDoc = person.CreateBuilder(workspace);
Person.Mutable root = updatedDoc.RootElement;
root.SetBirthDate(new LocalDate(1984, 6, 3));
Person updatedPerson = updatedDoc.RootElement;
Console.WriteLine(updatedPerson);
```

### Equality

```csharp
if (person == updatedPerson)
{
    Console.WriteLine("The same person.");
}
else
{
    Console.WriteLine("Different people.");
}

if (person == parsedFromUtf8.RootElement)
{
    Console.WriteLine("The same person.");
}
else
{
    Console.WriteLine("Different people.");
}
```

### String comparison — zero-allocation

```csharp
Console.WriteLine($"GivenName ValueEquals \"Hello\": {person.GivenName.ValueEquals("Hello")}");
Console.WriteLine($"GivenName ValueEquals \"Anne\"u8: {person.GivenName.ValueEquals("Anne"u8)}");
```

### Low-allocation access to character and byte data

```csharp
// Access character data via GetUtf16String()
using UnescapedUtf16JsonString utf16 = person.GivenName.GetUtf16String();
ReadOnlySpan<char> chars = utf16.Span;
int countA = chars.Count('A');
int countB = chars.Count('B');
Console.WriteLine($"Character counts in GivenName: A={countA}, B={countB}");

// Access UTF-8 byte data via GetUtf8String()
using UnescapedUtf8JsonString utf8 = person.GivenName.GetUtf8String();
ReadOnlySpan<byte> bytes = utf8.Span;
int countUtf8A = bytes.Count((byte)'A');
int countUtf8B = bytes.Count((byte)'B');
Console.WriteLine($"UTF-8 byte counts in GivenName: A={countUtf8A}, B={countUtf8B}");
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create directly with static factory method
Person person = Person.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.52);

// Parse from string
Person parsed = Person.Parse("{ ... }");

// Property access (same pattern)
string familyName = (string)person.FamilyName;
LocalDate date = person.BirthDate;

// Mutation via immutable With* methods
Person updated = person.WithBirthDate(new LocalDate(1984, 6, 3));
```

### V5 (Corvus.Text.Json)
```csharp
// Create using workspace and builder pattern
using JsonWorkspace workspace = JsonWorkspace.Create();
using var personDoc = Person.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.52);
Person person = personDoc.RootElement;

// Parse into immutable document wrapper
using var parsedDoc = ParsedJsonDocument<Person>.Parse(jsonString);
Person parsed = parsedDoc.RootElement;

// Property access (same pattern)
string familyName = (string)person.FamilyName;
LocalDate date = person.BirthDate;

// Mutation via mutable builder
using var updatedDoc = person.CreateBuilder(workspace);
Person.Mutable root = updatedDoc.RootElement;
root.SetBirthDate(new LocalDate(1984, 6, 3));
```

**Key differences:**
- V5 requires a `JsonWorkspace` for creation and mutation — this manages pooled memory for high-performance scenarios
- V5 uses `CreateBuilder(workspace, prop: value, ...)` instead of V4's `Create(prop: value, ...)`
- V5 wraps parsed results in `ParsedJsonDocument<T>` with `using` for deterministic memory management
- V5 mutation uses a mutable builder pattern (`SetProperty()`) instead of V4's immutable `WithProperty()` methods
- Property access patterns (`TryGetValue()`, `ValueEquals()`, `IsUndefined()`, implicit conversions) are the same in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/001-DataObject
dotnet run
```

## Related Patterns

- [002-DataObjectValidation](../002-DataObjectValidation/) - Adding validation constraints to data objects
- [003-ReusingCommonTypes](../003-ReusingCommonTypes/) - Sharing property types with `$ref` and `$defs`
- [005-ExtendingABaseType](../005-ExtendingABaseType/) - Inheritance via `allOf` with a single base

## Frequently Asked Questions

### How are JSON Schema property types mapped to .NET types?

Each property in the schema gets its own generated entity type (e.g., `Person.FamilyNameEntity` for a string property). These entity types provide implicit or explicit conversions to .NET primitives: `string` for `"type": "string"`, `double` for `"type": "number"`, `int` for `"type": "integer", "format": "int32"`, and so on. When a `format` keyword is present (e.g., `"format": "date"`), the entity type may also provide conversions to richer .NET types like `NodaTime.LocalDate`.

### What is the difference between `IsUndefined()` and `IsNull()`?

`IsUndefined()` returns `true` when a property was not present in the JSON document at all — the key is absent. `IsNull()` returns `true` when the property is present but its value is the JSON literal `null`. This distinction matters because JSON Schema treats missing properties and `null` values differently. For optional properties, always check `IsUndefined()` before accessing the value to avoid exceptions.

### Why does Corvus.Text.Json use NodaTime instead of `System.DateTime`?

JSON Schema date/time formats (`date`, `date-time`, `time`) map naturally to NodaTime types (`LocalDate`, `OffsetDateTime`, `LocalTime`) because NodaTime distinguishes between dates, times, and date-times with explicit offset handling — matching the semantics of the corresponding JSON Schema formats. `System.DateTime` conflates these concepts, which can lead to subtle timezone bugs. The generated types provide implicit conversions to the appropriate NodaTime types.

### How does zero-allocation string comparison work with `ValueEquals()`?

`ValueEquals()` compares the underlying JSON UTF-8 bytes directly against the provided `string` or `ReadOnlySpan<byte>` without allocating a .NET `string` from the JSON data. This is especially useful in hot paths where you need to check property values (e.g., routing, filtering) without the cost of string allocation. Use `ValueEquals("value"u8)` with a UTF-8 string literal for the best performance.

### When should I use the builder pattern vs. parsing from JSON?

Use `CreateBuilder(workspace, ...)` when you are constructing a new instance from .NET values — for example, building a response object in an API handler. Use `ParsedJsonDocument<T>.Parse(...)` when you receive JSON data from an external source (HTTP request body, file, message queue). The builder pattern requires a `JsonWorkspace` for pooled memory management, while parsing produces an immutable document that owns its own memory.
