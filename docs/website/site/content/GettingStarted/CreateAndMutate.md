---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Creating and Mutating Objects"
---
## Creating objects from scratch

Use the convenience `CreateBuilder()` overload with named property parameters:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var builder = Person.CreateBuilder(
    workspace,
    name: Person.PersonNameEntity.Build(
        (ref nb) => nb.Create(familyName: "Oldroyd"u8, givenName: "Michael"u8)),
    age: 30,
    email: "michael@example.com"u8);

Console.WriteLine(builder.RootElement.ToString());
// {"name":{"familyName":"Oldroyd","givenName":"Michael"},"age":30,"email":"michael@example.com"}
```

Required parameters must be provided; optional ones can be omitted. Always use named parameters for clarity and resilience to schema evolution.

### Nested objects

Use `NestedType.Build()` to compose nested values as parameters:

```csharp
using var builder = Person.CreateBuilder(
    workspace,
    name: Person.PersonNameEntity.Build(
        (ref nb) => nb.Create(familyName: "Oldroyd"u8, givenName: "Michael"u8)),
    age: 30,
    address: Person.AddressEntity.Build((ref ab) => ab.Create(
        street: "123 Main St"u8,
        city: "Springfield"u8)));
```

### Array properties

Build array properties by adding elements inside the builder delegate:

```csharp
using var builder = Person.CreateBuilder(
    workspace,
    name: Person.PersonNameEntity.Build(
        (ref nb) => nb.Create(familyName: "Oldroyd"u8, givenName: "Michael"u8)),
    hobbies: Person.HobbiesEntity.Build((ref hb) =>
    {
        hb.AddItem("reading"u8);
        hb.AddItem("hiking"u8);
        hb.AddItem("coding"u8);
    }));
```

### Advanced: delegate pattern

For scenarios that require logic inside the builder (e.g., conditional properties, `From()` conversions), use the delegate overload:

```csharp
using var builder = TargetType.CreateBuilder(workspace, (ref TargetType.Builder b) =>
{
    b.Create(
        fullName: TargetType.FullNameEntity.From(source.Name),
        identifier: TargetType.IdentifierEntity.From(source.Id));
});
```

## Mutating properties

Generated types are immutable by default. To mutate, create a `JsonDocumentBuilder` via a `JsonWorkspace`:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(
        """
        {
          "name": { "familyName": "Oldroyd", "givenName": "Michael" },
          "age": 30
        }
        """);
using var builder = doc.RootElement.CreateBuilder(workspace);

Person.Mutable root = builder.RootElement;
root.SetAge(31);
root.SetEmail("michael@example.com"u8);

Console.WriteLine(root.ToString());
// {"name":{"familyName":"Oldroyd","givenName":"Michael"},"age":31,"email":"michael@example.com"}
```

### Version tracking

The builder tracks a version number, and every `Mutable` element reference records the version at which it was obtained. If the document structure changes after you captured a reference, attempting to use that stale reference throws an `InvalidOperationException`.

You can make multiple modifications to the *same* entity without re-obtaining it:

```csharp
Person.Mutable root = builder.RootElement;
root.SetAge(31);
root.SetEmail("michael@example.com"u8);
root.Address.SetCity("London"u8);
```

A root element is always live, so it never needs to be re-obtained. Intermediate child references, however, *will* be invalidated by sibling mutations — always navigate from the root to access different children:

```csharp
Person.Mutable root = builder.RootElement;  // always live — cache freely
root.RemoveEmail();              // structural change
root.Address.SetCity("London"u8); // still valid — root is always live
```

To avoid expensive lookups, you can make multiple modifications to the *same* entity. Its own version is refreshed when it is modified.

```csharp
Person.Mutable root = builder.RootElement;  // always live — cache freely
root.RemoveEmail();              // structural change
Person.AddressEntity.Mutable address = root.Address;
address.SetCity("London"u8); // still valid — root is always live
address.SetZipCode("SE3"u8); // still valid — root is always live
```

## Removing properties

Optional properties can be removed from mutable instances:

```csharp
root.RemoveEmail();
```

## Copying values between documents

When the source and target properties share the same generated type, you can assign the value directly:

```csharp
using ParsedJsonDocument<Person> sourceDoc =
    ParsedJsonDocument<Person>.Parse(sourceJson);
using var targetBuilder = targetDoc.RootElement.CreateBuilder(workspace);

Person.Mutable target = targetBuilder.RootElement;

// Both documents use the same PersonNameEntity type — direct assignment
target.SetName(sourceDoc.RootElement.Name);
```

In practice, you often need to map between types generated from *different* schemas. Imagine a CRM system that defines its own `Employee` schema:

```json
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "properties": {
        "employeeId": { "type": "integer", "format": "int32" },
        "fullName": { "type": "string" },
        "workEmail": { "type": "string", "format": "email", "maxLength": 254 },
        "department": { "type": "string" }
    },
    "required": ["employeeId", "fullName", "workEmail"]
}
```

Both schemas have an email property based on `"type": "string", "format": "email"`, but the Employee schema adds a `maxLength` constraint. That extra constraint means the code generator produces a distinct `Employee.WorkEmailEntity` type rather than reusing the shared global email type. Because the underlying JSON is structurally compatible, you can use `TTarget.From()` to reinterpret the value without copying:

```csharp
using ParsedJsonDocument<Employee> employeeDoc =
    ParsedJsonDocument<Employee>.Parse(employeeJson);
using var personBuilder = personDoc.RootElement.CreateBuilder(workspace);

Person.Mutable person = personBuilder.RootElement;

// Employee.WorkEmailEntity and Person.EmailEntity are different C# types
// but both wrap "type": "string", "format": "email" — From() bridges them
person.SetEmail(Person.EmailEntity.From(employeeDoc.RootElement.WorkEmail));

// Simple string values can also be assigned directly via implicit conversion
person.Name.SetGivenName(employeeDoc.RootElement.FullName);
```

`From()` performs a zero-copy reinterpretation of the underlying JSON — no parsing or allocation occurs. It works for any structurally compatible types, including nested objects and arrays.

> **Important:** Setting a value in the target document does not copy the backing JSON data. Under the covers, it creates a reference to the relevant segment of the original UTF-8 JSON text. This makes cross-document property transfer very efficient for typical document processing pipelines — even for large nested objects or arrays, the cost is constant regardless of the size of the value.

## Composing objects with Apply()

When a schema uses `allOf` to compose multiple object definitions, the generated type exposes an `Apply()` method on its `Mutable` variant. This lets you merge properties from a composed type into the current object.

In our schema, `Address` is composed from a base `Location` (with `city` and `country`) and additional address properties (`street`, `zipCode`). You can build the location separately and apply it:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(
        """
        {
          "name": { "familyName": "Oldroyd" },
          "address": { "street": "123 Main St", "zipCode": "SP1 1AA" }
        }
        """);
using var builder = doc.RootElement.CreateBuilder(workspace);

// Parse a Location with city and country
using ParsedJsonDocument<Person.AddressEntity.LocationEntity> locationDoc =
    ParsedJsonDocument<Person.AddressEntity.LocationEntity>.Parse(
        """
        {
          "city": "Springfield",
          "country": "UK"
        }
        """);

// Apply merges the location properties into the address
Person.Mutable root = builder.RootElement;
root.Address.Apply(locationDoc.RootElement);

Console.WriteLine(root.Address.ToString());
// {"street":"123 Main St","zipCode":"SP1 1AA","city":"Springfield","country":"UK"}
```

`Apply()` iterates the properties of the composed type and merges them into the target object. If a property already exists, it is overwritten. You can call `Apply()` multiple times to layer properties from different composed types — this is the mutable equivalent of JSON Schema's `allOf` composition.

## Mutating arrays

Array properties on a `Mutable` element support in-place modification:

```csharp
Person.Mutable root = builder.RootElement;

// Add an item to the end
root.Hobbies.AddItem("gardening"u8);

// Insert at a specific index
root.Hobbies.InsertItem(0, "cooking"u8);

// Replace an item at an index
root.Hobbies.SetItem(1, "swimming"u8);

// Remove by index
root.Hobbies.RemoveAt(0);

// Add multiple items at once
root.Hobbies.AddRange(static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem("yoga"u8);
    b.AddItem("hiking"u8);
});

// Insert multiple items at a specific index
root.Hobbies.InsertRange(1, static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem("painting"u8);
    b.AddItem("music"u8);
});
```

The standard mutation workflow is:

1. Parse JSON into a `ParsedJsonDocument<T>`
2. Create a `JsonDocumentBuilder<T.Mutable>` via `.CreateBuilder(workspace)`
3. Get the `Mutable` root element from the builder
4. Call `Set*()` / `Remove*()` methods on the mutable element
5. Serialize via `root.WriteTo(writer)`, `root.ToString()`, or convert to immutable via `.Clone()`

## Serialization

Any generated type — whether parsed, built from scratch, or mutated — can be written back to JSON. The simplest approach allocates a `string`:

```csharp
// To a JSON string (allocates)
string json = person.ToString();
```

This is convenient for logging, debugging, or any situation where a managed `string` is required.

### Zero-allocation writing with pooled writers

For high-throughput scenarios where you want to avoid allocating strings, rent a `Utf8JsonWriter` and buffer from the workspace. The workspace manages a thread-local cache of writers and buffers, so repeated rent/return cycles are allocation-free:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

Utf8JsonWriter writer = workspace.RentWriterAndBuffer(
    defaultBufferSize: 1024,
    out IByteBufferWriter bufferWriter);
try
{
    person.WriteTo(writer);
    writer.Flush();

    // bufferWriter.WrittenSpan contains the UTF-8 JSON bytes
    ReadOnlySpan<byte> utf8Json = bufferWriter.WrittenSpan;
}
finally
{
    workspace.ReturnWriterAndBuffer(writer, bufferWriter);
}
```

Always return the writer and buffer in a `finally` block to ensure they are returned to the cache even if an exception occurs.

### Bring your own buffer

If you already have your own `IBufferWriter<byte>` (e.g. writing directly to a network stream or a pipeline), you can rent just the writer:

```csharp
var buffer = new ArrayBufferWriter<byte>();
Utf8JsonWriter writer = workspace.RentWriter(buffer);
try
{
    person.WriteTo(writer);
    writer.Flush();
}
finally
{
    workspace.ReturnWriter(writer);
}
```

Writer options such as indentation are configured once on the workspace via its `Options` property and applied to every rented writer automatically — no need to pass `JsonWriterOptions` each time.
