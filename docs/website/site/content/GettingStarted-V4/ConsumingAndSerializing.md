---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-29T00:00:00.0+00:00
Title: "Consuming and Serializing JSON"
---
## Consuming JSON — "Deserialization"

Our generated types are `readonly struct` wrappers over `System.Text.Json`'s `JsonElement`. They provide named property accessors with just-in-time conversion to .NET types — without copying the underlying UTF-8 bytes.

### Parsing from a JSON string

Add using statements and parse a JSON document:

```csharp
using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

string jsonText =
  """
  {
    "name": {
      "familyName": "Oldroyd",
      "givenName": "Michael",
      "otherNames": ["Francis", "James"]
    },
    "dateOfBirth": "1944-07-14"
  }
  """;

using JsonDocument document = JsonDocument.Parse(jsonText);
Person michaelOldroyd = new(document.RootElement);
```

Access properties with strongly-typed accessors:

```csharp
string familyName = (string)michaelOldroyd.Name.FamilyName;
string givenName = (string)michaelOldroyd.Name.GivenName;
LocalDate dateOfBirth = michaelOldroyd.DateOfBirth;

Console.WriteLine($"{familyName}, {givenName}: {dateOfBirth}");
// Output: Oldroyd, Michael: 14 July 1944
```

Creating the `Person` wrapper allocates almost nothing — it stores a reference to the `JsonElement` on the stack. String allocations only happen when you convert to .NET types.

### Using `JsonAny.Parse()` and `Person.Parse()`

You don't have to manage `JsonDocument` lifetime manually. `JsonAny.Parse()` clones the relevant segment and disposes the underlying document:

```csharp
Person michaelOldroyd = JsonAny.Parse(jsonText);
```

Or use the type-specific `Parse()` method directly:

```csharp
var michaelOldroyd = Person.Parse(jsonText);
```

### Type conversions

All types in `Corvus.Json` implement `IJsonValue`. Every generated type has implicit conversions to and from `JsonAny`, which enables zero-ceremony interop:

```csharp
// Any IJsonValue → JsonAny → Any other IJsonValue
JsonFoo myFoo = ...;
JsonBar myBar = myFoo.As<JsonBar>();
```

The code generator examines each schema and emits appropriate conversions. For example, `PersonNameElement` (a `string` schema) converts implicitly to `string`, and `JsonDate` converts implicitly to `NodaTime.LocalDate`.

> Converting between types doesn't guarantee validity — always validate if correctness matters.

## Serialization

All JSON types support `WriteTo(Utf8JsonWriter)` for efficient output:

```csharp
Utf8JsonWriter writer = ...;
michaelOldroyd.WriteTo(writer);
```

For quick inspection, use the `Serialize()` extension method:

```csharp
string serialized = michaelOldroyd.Serialize();
Console.WriteLine(serialized);
// {"name":{"familyName":"Oldroyd","givenName":"Michael","otherNames":["Francis","James"]},"dateOfBirth":"1944-07-14"}
```

> Prefer `WriteTo()` in production — it writes UTF-8 bytes directly to the output without string allocation. `Serialize()` is convenient for debugging but allocates a string.