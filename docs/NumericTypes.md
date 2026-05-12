# Numeric Types

This document explains the numeric type system in Corvus.JsonSchema V5, including the internal representation, how code generation selects types, and the precision model.

## Overview

JSON has a single `number` type with no precision limit. V5 embraces this by working with numbers in their original ASCII-coded decimal form — parsed into normalised components (sign, integral digits, fractional digits, exponent) — rather than converting to a fixed-precision .NET type.

The two key layers are:

1. **Parsed numeric components** — at the `JsonElement` level, numbers are stored as raw UTF-8 bytes and parsed on demand into `(isNegative, integral, fractional, exponent)` components for comparison and validation
2. **`BigNumber`** — an arbitrary-precision decimal struct (`significand × 10^exponent` where the significand is a `BigInteger`) used when a materialised numeric value is needed

## Internal representation: parsed components

When a JSON number needs to be compared, validated, or formatted, `JsonElementHelpers.Numeric.Core.TryParseNumber()` normalises the raw UTF-8 bytes into four components:

| Component | Type | Meaning |
|-----------|------|---------|
| `isNegative` | `bool` | Whether the number is negative |
| `integral` | `ReadOnlySpan<byte>` | UTF-8 integral digits, leading zeros removed |
| `fractional` | `ReadOnlySpan<byte>` | UTF-8 fractional digits, trailing zeros removed |
| `exponent` | `int` | Power-of-10 exponent |

The normalised form is: **value = sign × integral.fractional × 10^exponent**

Examples of normalisation:

| Input | isNegative | integral | fractional | exponent |
|-------|-----------|----------|------------|----------|
| `3.14` | false | `3` | `14` | -2 |
| `0.000123` | false | (empty) | `123` | -6 |
| `-42` | true | `42` | (empty) | 0 |
| `1.200e3` | false | `1` | `2` | 2 |
| `0` | false | (empty) | (empty) | 0 |

This representation preserves full precision — no information is lost regardless of the magnitude or number of significant digits. All comparison and validation operates on these components without ever converting to `double` or `decimal`.

### Number comparison

`CompareNormalizedJsonNumbers()` compares two numbers entirely in the ASCII domain:

1. Compare signs
2. Calculate effective magnitude from digit count + exponent
3. Compare digit-by-digit, accounting for exponent differences
4. No floating-point conversion — exact comparison at arbitrary precision

`AreEqualJsonNumbers()` follows the same pattern for equality checks.

## BigNumber

`BigNumber` is an arbitrary-precision decimal struct in `Corvus.Numerics`:

```csharp
public readonly partial struct BigNumber :
    IEquatable<BigNumber>, IComparable<BigNumber>,
    IFormattable, ISpanFormattable, IUtf8SpanFormattable,  // .NET 9+
    INumber<BigNumber>, ISignedNumber<BigNumber>           // .NET 9+
```

Internally: **value = significand × 10^exponent** where:
- **`Significand`** is a `BigInteger` (arbitrary precision)
- **`Exponent`** is an `int` (power of 10)

### When BigNumber is used

`BigNumber` is used when you need a materialised numeric value — for arithmetic, formatting with culture-specific patterns, or interop with APIs expecting a numeric type. The raw parsed-component representation is preferred for comparison and validation because it avoids allocation.

### Normalisation

`BigNumber.Normalize()` removes trailing zeros from the significand and adjusts the exponent:

```
1230 (significand=1230, exponent=0) → (significand=123, exponent=1)
```

### Formatting

`BigNumber` supports all standard .NET numeric format strings:

| Format | Description | Example (`BigNumber` 1234567) |
|--------|-------------|-------------------------------|
| G | General | `1234567` |
| F2 | Fixed, 2 decimal places | `1234567.00` |
| N0 | Number with grouping | `1,234,567` |
| E3 | Scientific | `1.235E+006` |
| C | Currency (culture-aware) | `$1,234,567.00` |
| P | Percent (×100, culture-aware) | `123,456,700.00%` |

Currency and percentage formatting use `NumberFormatInfo` for locale-specific symbols, separators, group sizes, and the full set of positive/negative patterns (16 negative currency patterns, 11 negative percent patterns, etc.).

### Parsing

`BigNumber.TryParse()` supports:
- Simple integers and decimals
- Scientific notation (`1.23e+10`)
- Currency symbols and thousands separators (via `NumberStyles`)
- Leading/trailing signs and parentheses for negatives
- DoS protection: input capped at 10,000 characters

Fast paths handle common cases (single digits, small integers fitting in `long`) before falling back to full `BigInteger` parsing.

### Stack allocation

`BigNumber` uses the standard pooling pattern with `StackAllocThreshold = 256`:

```csharp
Span<char> buffer = length <= StackAllocThreshold
    ? stackalloc char[StackAllocThreshold]
    : (rentedBuffer = ArrayPool<char>.Shared.Rent(length));
```

## Code generation type selection

The code generator reads the JSON Schema `format` keyword to select a .NET numeric type for typed accessors. The logic is in `TypeDeclarationExtensions.PreferredDotnetNumericTypeName()`:

| Format | .NET type | .NET-only? | netstandard2.0 fallback |
|--------|-----------|------------|------------------------|
| `"byte"` | `byte` | No | — |
| `"sbyte"` | `sbyte` | No | — |
| `"int16"` | `short` | No | — |
| `"int32"` | `int` | No | — |
| `"int64"` | `long` | No | — |
| `"int128"` | `Int128` | **Yes** | `long` |
| `"uint16"` | `ushort` | No | — |
| `"uint32"` | `uint` | No | — |
| `"uint64"` | `ulong` | No | — |
| `"uint128"` | `UInt128` | **Yes** | `ulong` |
| `"half"` | `Half` | **Yes** | `double` |
| `"single"` | `float` | No | — |
| `"double"` | `double` | No | — |
| `"decimal"` | `decimal` | No | — |
| (none, type=integer) | `long` | No | — |
| (none, type=number) | `double` | No | — |

The `NumericTypeName` struct captures this three-part mapping (name, isNetOnly, fallback) so the code generator can emit `#if NET` guards for platform-specific types.

## Runtime format validation

At runtime, format validation works directly on the parsed ASCII components — it never converts to `double` or `decimal` first. Each format has a `Match{Type}()` method in `JsonSchemaEvaluation.Number.cs`:

```csharp
public static bool MatchInt32(
    bool isNegative,
    ReadOnlySpan<byte> integral,
    ReadOnlySpan<byte> fractional,
    int exponent,
    ReadOnlySpan<byte> keyword,
    ref JsonSchemaContext context)
```

The method:
1. Checks `exponent >= 0` (must be an integer — no fractional component)
2. Compares against pre-computed ASCII-encoded bounds for `Int32.MinValue` / `Int32.MaxValue`
3. Reports the result via the `JsonSchemaContext`

This means a JSON number like `99999999999999999999` is correctly rejected as not fitting in `int32` without any precision loss from floating-point conversion.

### multipleOf validation

`multipleOf` also operates on the parsed components. For common divisors (1, 2, 3, 4, 5, 6, 8, 10), optimised fast paths avoid `BigInteger` arithmetic. For arbitrary divisors, it falls back to `BigInteger`-based modular arithmetic, ensuring exact results regardless of magnitude.

## Precision model

Because V5 works with ASCII-coded decimal components throughout, there are no precision boundaries for comparison, equality, or validation. A 500-digit JSON number is compared with perfect accuracy.

Precision limits only arise when you *materialise* a number into a fixed-precision .NET type:

| .NET type | Precision limit | Overflow range |
|-----------|----------------|----------------|
| `double` | ~15 significant digits | ±1.7 × 10³⁰⁸ |
| `decimal` | 28-29 significant digits | ±7.9 × 10²⁸ |
| `long` | Exact (19 digits max) | ±9.2 × 10¹⁸ |
| `BigNumber` | **Unlimited** | **Unlimited** |

Use `BigNumber` when you need arithmetic or formatting beyond the range of `decimal`.

## Guidelines

| Scenario | Recommended approach |
|----------|---------------------|
| Schema validation only | No format needed — works at full precision automatically |
| Typed accessors for integers | `"int32"` or `"int64"` format |
| Typed accessors for decimals | `"double"` (fast) or `"decimal"` (precise to 28 digits) |
| Financial/currency formatting | Use `BigNumber` with format string `"C"` |
| Arbitrary-precision arithmetic | Use `BigNumber` directly |
| Very large integers (.NET 9+) | `"int128"` / `"uint128"` format |
| Cross-platform (.NET + netstandard) | Avoid `"half"`, `"int128"`, `"uint128"` (or accept the fallback) |