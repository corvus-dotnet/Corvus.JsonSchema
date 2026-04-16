# ECMAScript Regex Translator

Zero-allocation translation of ECMAScript 262 regular expressions (with `/u` Unicode mode) to equivalent .NET regular expression patterns — operating entirely over `ReadOnlySpan<char>` with stack-allocated or pooled buffers.

This translator is used internally by the Corvus.Text.Json V5 code generator to translate JSON Schema `pattern` and `patternProperties` regular expressions from ECMAScript semantics to .NET semantics at code-generation time.

## Features

- **Zero Allocation** — Uses `ref struct` internals, `stackalloc`, and `ArrayPool<char>` rental (when a threshold is exceeded). No heap allocations on the translation path.
- **Full `/u` Mode Support** — Strict Unicode mode validation, including `\u{XXXXX}` code point escapes, `\p{...}` Unicode property escapes, and identity escape restrictions.
- **Semantic Translation** — Correctly handles the semantic differences between ECMAScript and .NET regex engines: `\d`, `\w`, `\s`, `\b`, `.` all have different meanings that are faithfully translated.
- **Supplementary Code Points** — Translates `\u{1F600}` to surrogate pair escapes, and handles supplementary character ranges in character classes with surrogate-pair-aware alternation patterns.
- **Conditional Backreferences** — ECMAScript treats non-participating capture groups as matching empty; .NET does not. Backreferences are translated to `(?(N)\N)` conditionals to match ECMAScript semantics.
- **Unicode Property Escapes** — General category long names mapped to short names, `Script=` mapped to .NET block names or explicit character ranges (e.g. `Script=Latin`), and binary properties (`ASCII`, `Alphabetic`, `White_Space`, `Emoji`, `Any`, `Assigned`) expanded to equivalent .NET character classes.
- **Character Class Strategies** — Handles negated shorthands in classes via alternation or subtraction, supplementary ranges via surrogate pair patterns, and alternation-pattern properties via `(?:...)` groups.

## Quick Start

```csharp
using Corvus.Text.Json.CodeGeneration;
using System.Text.RegularExpressions;

// Simple translation — allocates a string result
string dotNetPattern = EcmaRegexTranslator.Translate(@"\p{Script=Latin}+\s\d{2,4}");
Regex regex = new(dotNetPattern);
bool isMatch = regex.IsMatch("hello 42"); // true
```

### Zero-Allocation Path

For performance-critical code, use the `TryTranslate` method with a caller-provided buffer:

```csharp
using Corvus.Text.Json.CodeGeneration;
using System.Buffers;

ReadOnlySpan<char> ecmaPattern = @"\w+@\w+\.\w+";

// Calculate the maximum buffer size needed
int maxLen = EcmaRegexTranslator.GetMaxTranslatedLength(ecmaPattern);

// Stack-allocate for small patterns, rent for large ones
const int StackAllocThreshold = 256;
char[]? rented = null;
Span<char> buffer = maxLen <= StackAllocThreshold
    ? stackalloc char[StackAllocThreshold]
    : (rented = ArrayPool<char>.Shared.Rent(maxLen));

try
{
    OperationStatus status = EcmaRegexTranslator.TryTranslate(
        ecmaPattern, buffer, out int charsWritten);

    if (status == OperationStatus.Done)
    {
        ReadOnlySpan<char> dotNetPattern = buffer[..charsWritten];
        // Use the translated pattern...
    }
}
finally
{
    if (rented is not null)
    {
        ArrayPool<char>.Shared.Return(rented);
    }
}
```

### Error Handling

`TryTranslate` returns an `OperationStatus`:

| Status | Meaning |
|---|---|
| `Done` | Translation succeeded. |
| `DestinationTooSmall` | The destination buffer is too small. Use `GetMaxTranslatedLength` to size it correctly. |
| `InvalidData` | The ECMAScript pattern is invalid under `/u` mode strict rules. |

The convenience `Translate` method throws `ArgumentException` for invalid patterns and `InvalidOperationException` if the internal buffer estimation is too small (should never happen in practice).

## API Reference

### `EcmaRegexTranslator`

```csharp
internal static class EcmaRegexTranslator
{
    // Zero-allocation translation into a caller-provided buffer
    public static OperationStatus TryTranslate(
        ReadOnlySpan<char> ecmaPattern,
        Span<char> destination,
        out int charsWritten);

    // Convenience method that returns a string (uses stackalloc/ArrayPool internally)
    public static string Translate(ReadOnlySpan<char> ecmaPattern);

    // Calculates the maximum possible translated length for buffer sizing
    public static int GetMaxTranslatedLength(ReadOnlySpan<char> ecmaPattern);

    // Translation with silent fallback — returns the original pattern if translation fails
    public static string TranslateOrFallback(string ecmaPattern);
}
```
