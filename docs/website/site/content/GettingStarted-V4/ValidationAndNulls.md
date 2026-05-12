---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-29T00:00:00.0+00:00
Title: "Validation and Null Handling"
---
## Validation

JSON Schema uses a duck-typing model — it describes the *shape* of data rather than enforcing a rigid type system. When you construct a C# type from JSON data, it will be safe to use only if the data is *valid* according to the schema.

The code generator emits a `Validate()` method on each type. For simple pass/fail checks, use the `IsValid()` extension:

```csharp
bool isValid = michaelOldroyd.IsValid();
Console.WriteLine($"michaelOldroyd {(isValid ? "is" : "is not")} valid.");
// Output: michaelOldroyd is valid.
```

### Invalid data

Even when data is invalid, you can still access the parts that *are* present:

```csharp
string invalidJsonText =
    """
    {
      "name": {
        "givenName": "Michael",
        "otherNames": ["Francis", "James"]
      },
      "dateOfBirth": "1944-07-14"
    }
    """;

Person invalidPerson = JsonAny.Parse(invalidJsonText);
Console.WriteLine($"Is valid: {invalidPerson.IsValid()}");
// Output: Is valid: False

// The missing familyName makes it invalid, but we can still read what's there
string givenName = (string)invalidPerson.Name.GivenName;
Console.WriteLine(givenName);
// Output: Michael
```

> This forgiving approach is useful for diagnostics and self-healing scenarios. If you need to fail fast, validate first and throw an exception.

## Values, Null, and Undefined

JSON properties have three states:

```json
{ "foo": 3.14 }   // Present with a value
{ "foo": null }    // Present but null
{}                 // Not present (undefined)
```

Accessing a property that is undefined will throw when you try to convert it to a .NET type. Use the extension methods to check first:

```csharp
string givenName =
    michaelOldroyd.Name.GivenName.IsNotUndefined()
        ? (string)michaelOldroyd.Name.GivenName
        : "[no given name specified]";
```

Related extensions: `IsNull()`, `IsNullOrUndefined()`, `IsNotUndefined()`.

> **Nullable properties**: You can pass `--optionalAsNullable` to the code generator to emit optional properties as `Nullable<T>` directly, giving a more idiomatic .NET experience.

### Additional properties

JSON Schema allows additional properties by default. Access them with `TryGetProperty()`:

```csharp
if (michaelOldroyd.TryGetProperty("occupation", out JsonAny occupation) &&
    occupation.ValueKind == JsonValueKind.String)
{
    Console.WriteLine($"occupation: {occupation.AsString}");
}
```

### Enumerating properties

Use `EnumerateObject()` to iterate all properties. Filter out well-known properties using the generated `JsonPropertyNames` constants:

```csharp
foreach (JsonObjectProperty property in michaelOldroyd.EnumerateObject())
{
    if (property.NameEquals(Person.JsonPropertyNames.DateOfBirthUtf8) ||
        property.NameEquals(Person.JsonPropertyNames.NameUtf8))
    {
        continue; // Skip well-known properties
    }

    Console.WriteLine($"{(string)property.Name}: {property.Value}");
}
```

> The `NameEquals()` method with the pre-encoded `XXXUtf8` properties avoids allocating strings for property name comparisons.

### Working with arrays

Array types expose `EnumerateArray()`, `Length`, and indexer access:

```csharp
foreach (PersonNameElement otherName in michaelOldroyd.Name.OtherNames.EnumerateArray())
{
    Console.WriteLine(otherName);
}
// Output:
// Francis
// James
```

### Preserving information

Unlike code-first serializers, the V4 type model preserves all information through roundtrips — including additional properties, unknown extensions, and structural details — even when converting between types.