# Value types — brands, numbers, dates & times

JSON's primitive types are coarse: every string is a `string`, every number a `number`. JSON Schema refines them with `format` and numeric keywords, and the generator surfaces that refinement as **branded types**, **exact numeric evaluation**, and **`bigint`** for large integers. This guide covers working with those richer values.

## Branded format types

A `format` keyword generates a **branded** type and a validating factory:

```typescript
export type Email = Brand<string, "email">;
function fromEmail(value: string): Email {
  if (!__fmt("email", value)) { throw new FormatError("email"); }
  return value as Email;
}
export const Email = { evaluate: /* … */, from: fromEmail };   // companion: Email.from("…")
```

`Brand<string, "email">` is a `string` at run time but a distinct type at compile time, so a plain `string` is not assignable where an `Email` is expected. The only way to obtain one is through `Email.from`, which checks the format and throws `FormatError` on failure. Once a value has type `Email`, the compiler guarantees it is a well-formed email, so you do not need to validate it again downstream.

```typescript
const e: Email = Email.from("ada@example.com"); // ok
const bad: Email = "ada@example.com";        // compile error — a raw string is not an Email
```

When you read a value back out of an evaluated document, a branded field already has its brand type and carries through without further checking. `Type.evaluate` checks every format as part of whole-document validation, so the brand and the validator agree.

### Supported formats

The runtime ships RFC-accurate checks for:

`date` · `time` · `date-time` · `duration` · `email` · `idn-email` · `hostname` · `idn-hostname` · `ipv4` · `ipv6` · `uuid` · `uri` · `iri` · `uri-reference` · `iri-reference` · `uri-template` · `json-pointer` · `relative-json-pointer` · `regex`

The internationalised forms (`idn-*`, `iri-*`) use proper IDNA processing; `uuid`, `ipv6` and the URI family follow their RFCs rather than a loose regex.

Numeric `format`s brand a **number** rather than a string — see [Numeric format brands](#numeric-format-brands) below.

## Numbers

A `number` or `integer` reads as a TypeScript `number` by default. Two things make numeric handling exact rather than "good enough":

### Exact evaluation

Numeric keywords — `minimum`, `maximum`, `exclusiveMinimum/Maximum`, `multipleOf` — are evaluated against the number's **source text** using big-integer arithmetic (a decimal is decomposed into sign, integer mantissa, and base-10 exponent), not by comparing IEEE-754 doubles. So the cases where floating point lies just work:

```typescript
Type.evaluate({ score: 0.3 });  // multipleOf 0.5 -> false  (0.3 is not a multiple of 0.5)
// even though, in raw JS, 0.3 % 0.1 === 0.05 rather than 0
```

Bounds on very large integers are likewise compared by mathematical value, so `9007199254740993` is distinct from `9007199254740992` even though both round to the same double.

### Large integers → `bigint`

An integer `format` that exceeds JavaScript's safe-integer range generates `bigint` rather than `number`:

| format | TypeScript |
|--------|------------|
| `int64` · `uint64` · `int128` · `uint128` | `bigint` |

```typescript
const v = JSON.parse(text) as Values;
v.id;            // bigint — build and compare with bigint literals (123n)
```

### Numeric format brands

A sub-64-bit numeric `format` brands a **`number`** (the value fits a JS double) and emits a validating `as{Name}` factory, the numeric analog of the string-format brands above. The factory enforces the format's range (and integer-ness, where applicable); `Type.evaluate` enforces the same range when `format` is asserted.

| format | TypeScript | factory check |
|--------|------------|---------------|
| `byte` · `sbyte` · `int16` · `uint16` · `int32` · `uint32` | `Brand<number, "fmt">` | integer, in range (e.g. `byte` 0–255, `int32` ±2³¹) |
| `half` | `Brand<number, "half">` | in range ±65504 (fractions allowed) |
| `single` · `double` | `Brand<number, "fmt">` | unbounded (a type tag; any number) |
| `decimal` | `Brand<number, "decimal">` | in the .NET decimal range |

```typescript
const port = Port.from(8080);   // Port = Brand<number, "uint16">; throws on -1 or 70000
const level = Level.from(255);  // Level = Brand<number, "byte">;  throws on 256 or 1.5
```

### Typed-array views

For a dense numeric array (`readonly number[]`) in performance-sensitive code, the runtime offers optional typed-array views. The generated array type stays `readonly number[]`; the views are opt-in:

```typescript
import { toFloat64Array, toFloat32Array, toInt32Array } from "@endjin/corvus-json-runtime";
const speeds = toFloat64Array(reading.samples); // a Float64Array over the values
```

### Arbitrary precision

The declared type for an ordinary `number` is `number`, but the evaluator also accepts `lossless-json` numbers, so it is exact on the original digits even past double precision. To read those exact digits as a **value** (for a big-number library, a content hash, or a cache key), parse with the runtime's re-exported `parseLossless` and call `exactNumber` — the zero-dependency big-number seam (feed the string into decimal.js / bignumber.js):

```typescript
import { parseLossless, exactNumber } from "@endjin/corvus-json-runtime";
const doc = parseLossless(text) as { amount: unknown };
exactNumber(doc.amount); // "123456789012345678901234567890.5" — JSON.parse would have rounded it
```

A `decimal`-format field additionally gets a generated `{Type}.toExact(value): string` accessor that delegates to `exactNumber`. (Parse with `parseLossless` to preserve the digits; a plain `JSON.parse` has already rounded.)

## Dates, times and durations

`date`, `time` and `date-time` are validated to **RFC 3339** (calendar-valid dates, leap-second `:60`, offsets, fractional seconds, `T`/`Z` casing). `duration` follows the **RFC 3339 ABNF** grammar, which is *stricter* than ISO 8601 / `Temporal.Duration` — units must be contiguous and descending and there are no fractions (`P1Y2D` and `PT1H2S` are invalid).

A formatted date/time field is a **branded string** — `Brand<string, "date-time">` — built and checked with its `as*` factory:

```typescript
const when = When.from("2026-06-26T10:00:00Z"); // When = Brand<string, "date-time">
```

Each of the four temporal formats gets a generated `{Type}.toTemporal` accessor that parses the branded string into its matching [`Temporal`](https://tc39.es/proposal-temporal/docs/) value, so you don't convert by hand:

```typescript
Day.toTemporal(account.day);          // Temporal.PlainDate   (format: date)
OccurredAt.toTemporal(event.at);      // Temporal.Instant     (format: date-time — the absolute instant)
AtTime.toTemporal(slot.start);        // Temporal.PlainTime   (format: time)
Span.toTemporal(plan.duration);       // Temporal.Duration    (format: duration)
```

`Temporal` is re-exported from the runtime, so you get the types from the package. (You can still convert the branded string by hand — `new Date(account.created)` or `Temporal.Instant.from(account.created)` — if you prefer.)

## See also

- [examples/019-formats](./examples/019-formats/) — branded formats, runnable.
- [examples/002-validation](./examples/002-validation/) — numeric constraints in context.
- [getting-started](./getting-started.md) — the `lossless-json` / `@js-temporal/polyfill` runtime dependencies.
