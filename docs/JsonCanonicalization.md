# JSON Canonicalization Scheme (RFC 8785)

## Overview

`Corvus.Text.Json` implements [RFC 8785 JSON Canonicalization Scheme (JCS)](https://datatracker.ietf.org/doc/html/rfc8785), which defines a deterministic serialization of JSON values. This enables JSON data to be used in contexts that require byte-exact representations — such as digital signatures, content hashing, and content-addressed storage.

The implementation lives in the `Corvus.Text.Json.Canonicalization` namespace and operates directly on the `JsonElement` document model with zero heap allocation on the hot path.

## Installation

JCS is included in the core library — no additional package is needed:

```bash
dotnet add package Corvus.Text.Json
```

## Quick Start

```csharp
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

string json = """{"z": 1, "a": 2, "m": 3}""";

using var doc = ParsedJsonDocument<JsonElement>.Parse(json);

// Option 1: Write to a caller-provided buffer (zero-allocation)
Span<byte> buffer = stackalloc byte[256];
bool success = JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out int bytesWritten);
string canonical = Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten));
Console.WriteLine(canonical);
// {"a":2,"m":3,"z":1}

// Option 2: Get as a byte array (allocates the result array)
byte[] canonicalBytes = JsonCanonicalizer.Canonicalize(doc.RootElement);
Console.WriteLine(Encoding.UTF8.GetString(canonicalBytes));
// {"a":2,"m":3,"z":1}
```

## Canonicalization Rules

RFC 8785 specifies these deterministic serialization rules:

| Aspect | Rule |
|--------|------|
| **Property ordering** | Object properties are sorted by UTF-16 code unit values of their names |
| **Number formatting** | Numbers use ECMAScript `Number.toString()` formatting (IEEE 754 double precision) |
| **String escaping** | Minimal escaping: only `\"`, `\\`, and control characters (`\b`, `\t`, `\n`, `\f`, `\r`, `\uXXXX` for remaining C0 controls) |
| **Whitespace** | No whitespace between structural tokens |

### I-JSON Compliance

JCS requires input to conform to [I-JSON (RFC 7493)](https://datatracker.ietf.org/doc/html/rfc7493):

- **No duplicate property names** — `TryCanonicalize` throws `InvalidOperationException` if duplicates are detected.
- **Numbers must be IEEE 754 double-precision representable** — `TryCanonicalize` throws `InvalidOperationException` for numbers that cannot be converted to `double`, or for `NaN`/`Infinity` values.

## API Reference

### `TryCanonicalize`

```csharp
public static bool TryCanonicalize(
    in JsonElement element,
    Span<byte> destination,
    out int bytesWritten)
```

Canonicalizes a JSON element and writes the result to the provided buffer. Returns `true` if the output fits; `false` if the buffer is too small (in which case `bytesWritten` contains the number of bytes written before overflow). This method performs no heap allocation.

### `Canonicalize`

```csharp
public static byte[] Canonicalize(in JsonElement element)
```

Convenience method that returns the canonical form as a new byte array. Internally tries a stack-allocated buffer first, then grows via `ArrayPool` if needed. The returned array is always a fresh allocation sized to the exact output length.

## Use Cases

### Digital Signatures

Canonicalize before signing to ensure the same logical document always produces the same signature:

```csharp
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

string json = """{"alg": "HS256", "data": {"b": 2, "a": 1}}""";

using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
byte[] canonical = JsonCanonicalizer.Canonicalize(doc.RootElement);

byte[] hash = SHA256.HashData(canonical);
Console.WriteLine(Convert.ToHexStringLower(hash));
```

### Content-Addressed Storage

Use the canonical hash as a storage key:

```csharp
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
byte[] canonical = JsonCanonicalizer.Canonicalize(doc.RootElement);

string key = Convert.ToHexStringLower(SHA256.HashData(canonical));
```

