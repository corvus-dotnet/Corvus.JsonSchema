---
name: corvus-numeric-types
description: >
  Understand and work with the Corvus.Text.Json numeric type system including parsed
  components, BigNumber arbitrary-precision decimal, format-based type selection, and
  precision-preserving validation. USE FOR: working with JSON numbers in Corvus types,
  understanding format validation for numeric schemas, implementing numeric operations,
  choosing between BigNumber and standard .NET numeric types.
  DO NOT USE FOR: general JSON parsing (use corvus-parsed-documents-and-memory).
---

# Numeric Types

## Parsed Component Representation

JSON numbers are stored as raw UTF-8 bytes and parsed on demand into four components:

```
(isNegative, integral, fractional, exponent)
```

Example: `-123.456e+7` → `(true, "123", "456", "+7")`

**All comparison and validation operates on ASCII digit components — no floating-point conversion.** This preserves full precision and avoids IEEE 754 rounding.

## BigNumber

An arbitrary-precision decimal: `significand × 10^exponent` where significand is `BigInteger`.

```csharp
// From string
BigNumber bn = BigNumber.Parse("123456789012345678901234567890.123");

// From JSON element
BigNumber bn = element.GetBigNumber();
```

**When to use BigNumber:**
- JSON values with > 28 significant digits (beyond `decimal` precision)
- When exact precision matters (financial, scientific)
- When you need DoS-safe parsing (input capped at 10,000 characters)

**Supports all standard .NET format strings:** G, F, N, E, C, P, R.

On .NET 9+, `BigNumber` implements `INumber<BigNumber>` for full generic math support.

## Format-to-Type Mapping

The JSON Schema `format` keyword maps to .NET types:

| Format | .NET Type | Notes |
|--------|-----------|-------|
| `int32` | `int` | |
| `int64` | `long` | |
| `int128` | `Int128` | .NET 7+ only, netstandard2.0 fallback |
| `uint32` | `uint` | |
| `uint64` | `ulong` | |
| `uint128` | `UInt128` | .NET 7+ only |
| `half` | `Half` | .NET 5+ only |
| `single` | `float` | |
| `double` | `double` | |
| `decimal` | `decimal` | |
| `byte` | `byte` | |
| `sbyte` | `sbyte` | |
| `int16` | `short` | |
| `uint16` | `ushort` | |

`Int128`, `UInt128`, and `Half` have netstandard2.0 fallbacks.

## Validation Optimizations

### multipleOf Fast Paths

Common divisors have optimized implementations:
- Divisors 1, 2, 3, 4, 5, 6, 8, 10 use specialized integer arithmetic
- Other divisors fall back to `BigNumber` division

### Range Validation

`minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum` all operate on
parsed components (no float conversion), preserving exact precision.

## DoS Protection

Numeric input is capped at **10,000 characters** (`BigNumber.MaxInputLength` in `src/Corvus.Text.Json/Corvus/Numerics/BigNumber.cs`) to prevent resource-exhaustion attacks with extremely large numbers.

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `Corvus.Numerics` | `BigNumber`, `BigInteger` support types |
| `Corvus.Text.Json` | JSON element numeric accessors |

## Common Pitfalls

- **Don't use `decimal` for arbitrary precision**: `decimal` has 28-29 significant digits max. Use `BigNumber` for unbounded precision.
- **Don't convert to `double` for comparison**: IEEE 754 rounds. Use parsed-component comparison.
- **netstandard2.0 limitations**: `Int128`/`UInt128`/`Half` aren't available — fallbacks exist but with reduced functionality.

## Cross-References
- For parsing JSON documents, see `corvus-parsed-documents-and-memory`
- For format validation in code generation, see `corvus-keywords-and-validation`
- Full guide: `docs/NumericTypes.md`
