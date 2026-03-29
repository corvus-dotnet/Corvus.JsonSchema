---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Using Generated Types"
---
## Parsing

### ParsedJsonDocument (preferred)

`ParsedJsonDocument<T>` manages a pooled-memory document. Dispose it when you're done:

```csharp
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(
        """{"name":{"familyName":"Oldroyd","givenName":"Michael"},"age":30}""");
Person person = doc.RootElement;
```

### ParseValue (convenience)

Returns a self-owned value — no explicit disposal needed, but the backing memory is not shared:

```csharp
Person person = Person.ParseValue(
    """{"name":{"familyName":"Oldroyd","givenName":"Michael"},"age":30}""");
```

### Other sources

```csharp
// From UTF-8 bytes
Person person = Person.ParseValue(
    """{"name":{"familyName":"Oldroyd"},"age":30}"""u8);

// From a stream
using FileStream stream = File.OpenRead("person.json");
using ParsedJsonDocument<Person> doc = ParsedJsonDocument<Person>.Parse(stream);
```

## Property access

Each schema property generates a typed accessor returning a nested entity struct:

```csharp
Person person = doc.RootElement;

// Required property — name is a nested PersonName object
// Strings require an explicit cast (or GetString())
string familyName = (string)person.Name.FamilyName;

// Value types like int support implicit conversion — no cast needed
if (person.Age.IsNotUndefined())
{
    int age = person.Age;
}

if (person.Email.IsNotUndefined())
{
    string email = (string)person.Email;
}
```

### Property indexers

Access properties dynamically by name, returning a `JsonElement`:

```csharp
// UTF-8 string literal (preferred — avoids transcoding)
JsonElement name = person["name"u8];

// String overload also available (transcodes to UTF-8 internally)
JsonElement age = person["age"];
```

### TryGetProperty

```csharp
if (person.TryGetProperty("email"u8, out JsonElement email))
{
    Console.WriteLine((string)email);
}
```

## Array access

Our Person schema has two array types: `hobbies` is a simple `"type": "array"` with `"items": { "type": "string" }`, and `PersonNameElementArray` (used by `otherNames` via `oneOf`) is an array of `PersonNameElement` strings. The code generator produces strongly-typed array wrappers for both.

### Enumerating array elements

The generated type supports `foreach` via `EnumerateArray()`:

```csharp
foreach (var hobby in person.Hobbies.EnumerateArray())
{
    Console.WriteLine((string)hobby);
}
```

When `otherNames` is an array (rather than a single string), you can enumerate it in the same way:

```csharp
// otherNames uses oneOf — it can be a single string or an array
// Use AsPersonNameElementArray to access the array variant
foreach (var name in person.Name.OtherNames.AsPersonNameElementArray.EnumerateArray())
{
    Console.WriteLine((string)name);
}
```

The enumerator also supports LINQ:

```csharp
var hobbies = person.Hobbies.EnumerateArray()
    .Select(h => (string)h)
    .ToList();
```

### Array length and indexing

```csharp
int count = person.Hobbies.GetArrayLength();

// Access by index
var first = person.Hobbies[0];
string firstHobby = (string)first;
```

## How schema properties map to .NET types

### String types

| Schema type | Format | .NET type |
|---|---|---|
| `"type": "string"` | *(none)* | `string` via `GetString()` or explicit cast |
| `"type": "string"` | `date` | `LocalDate` (NodaTime) |
| `"type": "string"` | `date-time` | `OffsetDateTime` (NodaTime) / `DateTimeOffset` |
| `"type": "string"` | `time` | `OffsetTime` (NodaTime) |
| `"type": "string"` | `duration` | `Period` (NodaTime) |
| `"type": "string"` | `uuid` | `Guid` |
| `"type": "string"` | `uri` | `Utf8UriValue` |
| `"type": "string"` | `uri-reference` | `Utf8UriReferenceValue` |
| `"type": "string"` | `uri-template` | `string` |
| `"type": "string"` | `iri` | `Utf8IriValue` |
| `"type": "string"` | `iri-reference` | `Utf8IriReferenceValue` |
| `"type": "string"` | `email` | `string` (RFC 5322 validated) |
| `"type": "string"` | `idn-email` | `string` (internationalized email) |
| `"type": "string"` | `hostname` | `string` (RFC 1123 validated) |
| `"type": "string"` | `idn-hostname` | `string` (internationalized hostname) |
| `"type": "string"` | `ipv4` | `string` (IPv4 validated) |
| `"type": "string"` | `ipv6` | `string` (IPv6 validated) |
| `"type": "string"` | `json-pointer` | `string` (RFC 6901 validated) |
| `"type": "string"` | `relative-json-pointer` | `string` |
| `"type": "string"` | `regex` | `string` (ECMAScript regex) |

### Integer types

| Schema type | Format | .NET type |
|---|---|---|
| `"type": "integer"` | *(none)* | `long` via `GetInt64()` |
| `"type": "integer"` | `byte` | `byte` |
| `"type": "integer"` | `sbyte` | `sbyte` |
| `"type": "integer"` | `int16` | `short` |
| `"type": "integer"` | `int32` | `int` |
| `"type": "integer"` | `int64` | `long` |
| `"type": "integer"` | `int128` | `Int128` (.NET 7+) |
| `"type": "integer"` | `uint16` | `ushort` |
| `"type": "integer"` | `uint32` | `uint` |
| `"type": "integer"` | `uint64` | `ulong` |
| `"type": "integer"` | `uint128` | `UInt128` (.NET 7+) |

### Number types

| Schema type | Format | .NET type |
|---|---|---|
| `"type": "number"` | *(none)* | `double` via `GetDouble()` |
| `"type": "number"` | `half` | `Half` (.NET 5+) |
| `"type": "number"` | `single` | `float` |
| `"type": "number"` | `double` | `double` |
| `"type": "number"` | `decimal` | `decimal` |

### Other types

| Schema type | Format | .NET type |
|---|---|---|
| `"type": "boolean"` | *(none)* | `bool` |
| `"type": "object"` | *(none)* | Generated nested struct |
| `"type": "array"` | *(none)* | Generated array type with enumeration |

## Converting to .NET types

### Implicit conversions (value types)

For .NET value types (`int`, `double`, `bool`, `DateTime`, etc.), the generated types support **implicit** conversion — no cast syntax needed:

```csharp
int age = person.Age;
double score = person.Score;
bool isActive = person.IsActive;
```

This is the most concise approach and works for all value types.

### Explicit cast (strings)

`string` requires an **explicit** cast by default, because every conversion allocates a new `string`:

```csharp
string familyName = (string)person.Name.FamilyName;
```

> **Tip:** You can opt in to implicit `string` conversion via the `--optionalStringImplicit` flag on the CLI tool, or the `CorvusJsonSchemaOptionalStringImplicit` MSBuild property in your `.csproj` for the source generator. This trades allocation safety for convenience — use it when you know you need the string and are not in a hot path.

### TryGetValue

A safe, non-throwing alternative that returns `false` if the value is absent or cannot be converted:

```csharp
if (person.Age.TryGetValue(out int age)) { ... }
if (person.Name.FamilyName.TryGetValue(out string? name)) { ... }
```

### GetString, GetUtf8String, and GetUtf16String

For string-valued properties:

```csharp
// As a .NET string (allocates)
string familyName = person.Name.FamilyName.GetString();

// As an UnescapedUtf8JsonString (avoids allocation — always dispose)
using UnescapedUtf8JsonString utf8Name = person.Name.FamilyName.GetUtf8String();
ReadOnlySpan<byte> bytes = utf8Name.Span;

// As an UnescapedUtf16JsonString (avoids allocation — always dispose)
using UnescapedUtf16JsonString utf16Name = person.Name.FamilyName.GetUtf16String();
ReadOnlySpan<char> chars = utf16Name.Span;
```

`GetUtf8String()` gives you the raw UTF-8 bytes without allocating a `string`. `GetUtf16String()` gives you a `char` span — useful when you need to interop with APIs that expect `ReadOnlySpan<char>` without paying for a `string` allocation. Both return disposable types that must be disposed to return their buffers to the pool.

## Values, Null, and Undefined

JSON values exist in three states:

```json
{ "foo": 3.14 }   // Present with a value
{ "foo": null }    // Present but null
{}                 // Not present ("undefined")
```

Generated types expose this three-state model:

```csharp
if (person.Email.IsNotUndefined())
{
    // The property exists in the JSON (may still be null)
    string email = (string)person.Email;
}

person.Email.IsUndefined()          // true if the property is absent
person.Email.IsNull()               // true if the property is present but null
person.Email.IsNullOrUndefined()    // true if null or absent
person.Email.IsNotNullOrUndefined() // true if present with a non-null value
```

When you attempt to cast an undefined or null value to a .NET type, it throws an `InvalidOperationException`. Always check before casting optional properties.

## Equality and comparison

Generated types support value equality:

```csharp
Person a = Person.ParseValue(json);
Person b = Person.ParseValue(json);

bool equal = a.Equals(b);  // true — deep JSON equality
bool same  = a == b;        // operator overload
```

## Composition types and pattern matching

JSON Schema supports composition keywords like `oneOf`, `anyOf`, and `allOf` that let a value match one of several shapes. In our schema, `OtherNames` uses `oneOf` — it can be *either* a single `PersonNameElement` string *or* a `PersonNameElementArray`:

```json
"OtherNames": {
    "oneOf": [
        { "$ref": "#/$defs/PersonNameElement" },
        { "$ref": "#/$defs/PersonNameElementArray" }
    ]
}
```

The generated type provides a `Match()` method that dispatches to a typed delegate for each variant. Each variant gets its own named parameter, and a `defaultMatch` fallback handles values that don't conform to any variant:

```csharp
string result = person.Name.OtherNames.Match(
    matchPersonNameElement: static (v) => $"Single name: {(string)v}",
    matchPersonNameElementArray: static (v)
        => $"Multiple names: {string.Join(", ", v.EnumerateArray().Select(n => (string)n))}",
    defaultMatch: static (_) => "Unknown format");

Console.WriteLine(result);
```

`Match()` evaluates each variant's schema in order, calls the first matching delegate, and returns the result. All delegates must return the same type (`TResult`), which the compiler infers from usage.

There is also a context-passing overload for when you need to pass state into the matchers without capturing:

```csharp
string result = person.Name.OtherNames.Match(
    separator,  // context passed to each matcher
    matchPersonNameElement: static (v, sep) => (string)v,
    matchPersonNameElementArray: static (v, sep)
        => string.Join(sep, v.EnumerateArray().Select(n => (string)n)),
    defaultMatch: static (_, sep) => string.Empty);
```
