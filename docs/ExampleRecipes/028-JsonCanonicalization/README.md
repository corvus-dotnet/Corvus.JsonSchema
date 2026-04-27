# JSON Schema Patterns in .NET - JSON Canonicalization (RFC 8785)

This recipe demonstrates how to use [RFC 8785 JSON Canonicalization Scheme (JCS)](https://datatracker.ietf.org/doc/html/rfc8785) with the `Corvus.Text.Json` library. JCS provides a deterministic serialization of JSON values, enabling byte-exact comparison, content hashing, and digital signature use cases.

## The Pattern

JCS defines a canonical form for JSON that ensures the same logical document always serializes to the same byte sequence. The rules are:

- **Property ordering**: Object properties are sorted by UTF-16 code unit values of their names
- **Number formatting**: Numbers use the ECMAScript `Number.toString()` algorithm
- **Minimal string escaping**: Only required characters are escaped (`\"`, `\\`, and control characters)
- **No whitespace**: No spaces or newlines between structural tokens

## Generated Code Usage

[Example code](./Program.cs)

### Zero-allocation canonicalization

Use `TryCanonicalize` with a caller-provided buffer for zero heap allocation:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

string json = """{"z": 1, "a": 2, "m": 3}""";
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);

Span<byte> buffer = stackalloc byte[256];
bool success = JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out int bytesWritten);
// buffer[..bytesWritten] contains: {"a":2,"m":3,"z":1}
```

### Content hashing

Different representations of the same logical document produce the same canonical form:

```csharp
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

string variant1 = """{"b": 2, "a": 1}""";
string variant2 = """{"a":1,"b":2}""";

using var doc1 = ParsedJsonDocument<JsonElement>.Parse(variant1);
using var doc2 = ParsedJsonDocument<JsonElement>.Parse(variant2);

byte[] hash1 = SHA256.HashData(JsonCanonicalizer.Canonicalize(doc1.RootElement));
byte[] hash2 = SHA256.HashData(JsonCanonicalizer.Canonicalize(doc2.RootElement));
// hash1 and hash2 are identical
```

## Running the Example

```bash
cd docs/ExampleRecipes/028-JsonCanonicalization
dotnet run
```

## Related Patterns

None currently.
