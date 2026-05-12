# JSON Schema Patterns in .NET - Creating and Mutating Objects

This recipe demonstrates the full mutable document lifecycle using the V5 API: parsing an immutable document, creating a mutable builder, reading and modifying properties, mutating nested arrays, and serializing the result.

## The Pattern

The V5 API separates immutable and mutable representations of JSON documents. You start with an immutable `ParsedJsonDocument<T>`, create a mutable `JsonDocumentBuilder<T.Mutable>` via a `JsonWorkspace`, and then modify properties, add or remove array items, and serialize the result. The workspace manages pooled memory so that mutations are efficient and allocation-free in the hot path.

Sub-element references (e.g. a mutable array obtained from a property) track the document version and become stale after mutations. Always re-fetch sub-elements from the root after modifying the document.

## The Schema

File: `person.json`

```json
{
    "title": "Person",
    "type": "object",
    "required": ["name", "age"],
    "properties": {
        "name": {
            "type": "object",
            "required": ["familyName"],
            "properties": {
                "familyName": { "type": "string" },
                "givenName": { "type": "string" }
            }
        },
        "age": { "type": "integer", "format": "int32" },
        "email": { "type": "string", "format": "email" },
        "hobbies": {
            "type": "array",
            "items": { "type": "string" }
        }
    }
}
```

The generated .NET properties are:

- `Name` — of type `Person.NameEntity` (a nested object with `FamilyName` and `GivenName`)
- `Age` — of type `Person.AgeEntity` (an int32 type)
- `Email` — of type `Person.EmailEntity` (an email-formatted string type)
- `Hobbies` — of type `Person.HobbiesEntity` (an array of strings)

## Generated Code Usage

[Example code](./Program.cs)

### Parsing an immutable document

```csharp
string personJson =
    """
    {
        "name": {
            "familyName": "Smith",
            "givenName": "John"
        },
        "age": 30,
        "email": "john@example.com",
        "hobbies": ["reading", "hiking"]
    }
    """;

using var parsedDoc = ParsedJsonDocument<Person>.Parse(personJson);
Person person = parsedDoc.RootElement;
```

### Creating a mutable builder

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = person.CreateBuilder(workspace);
Person.Mutable root = builder.RootElement;
```

### Reading properties from the mutable root

```csharp
Console.WriteLine($"  Name: {root.Name.GivenName} {root.Name.FamilyName}");
Console.WriteLine($"  Age: {root.Age}");
Console.WriteLine($"  Email: {root.Email}");
```

### Setting properties

```csharp
root.SetAge(31);
root.SetEmail("john.smith@example.com");
```

### Removing an optional property

```csharp
bool removed = root.RemoveEmail();
```

### Mutating arrays — add, insert, and remove

```csharp
// Add a hobby at the end
var hobbies = root.Hobbies;
hobbies.AddItem("cooking");

// Insert a hobby at the beginning (re-fetch after prior mutation)
hobbies = root.Hobbies;
hobbies.InsertItem(0, "painting");

// Remove the hobby at index 1 (re-fetch after prior mutation)
hobbies = root.Hobbies;
hobbies.RemoveAt(1);
```

### Version tracking

The root element is always live after mutations — you can always read the current state from `builder.RootElement`. Sub-element references (like `hobbies` above) must be re-fetched from the root after each mutation because the document version changes.

```csharp
Person current = builder.RootElement;
Console.WriteLine($"  Current age: {current.Age}");
Console.WriteLine($"  Hobbies count: {current.Hobbies.GetArrayLength()}");
```

### Serialization

```csharp
// Convert to JSON string (allocates)
string jsonString = builder.RootElement.ToString();

// Write to a Utf8JsonWriter (does not allocate)
ArrayBufferWriter<byte> abw = new();
using (Utf8JsonWriter writer = new(abw))
{
    builder.RootElement.WriteTo(writer);
    writer.Flush();
}
```

## Running the Example

```bash
cd docs/ExampleRecipes/018-CreatingAndMutatingObjects
dotnet run
```

## Related Patterns

- [001-DataObject](../001-DataObject/) — Simple data objects with property access, equality, and basic mutation
- [019-CloneAndFreeze](../019-CloneAndFreeze/) — Cloning mutable elements into independent immutable documents