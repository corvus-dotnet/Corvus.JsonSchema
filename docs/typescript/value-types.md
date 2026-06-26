# Value types — brands, numbers, dates & times

JSON's primitive types are coarse: every string is a `string`, every number a `number`. JSON Schema refines them with `format` and numeric keywords, and the generator surfaces that refinement as **branded types**, **exact numeric evaluation**, and **`bigint`** for large integers. This guide covers working with those richer values.

## Branded format types

A `format` keyword generates a **branded** type and a validating factory:

```typescript
export type Email = Brand<string, "email">;
export function asEmail(value: string): Email {
  if (!__fmt("email", value)) { throw new FormatError("email"); }
  return value as Email;
}
```

`Brand<string, "email">` is a `string` at runtime but a *distinct* type at compile time, so a plain `string` is **not** assignable where an `Email` is expected. The only way to get one is through `asEmail`, which checks the format and throws `FormatError` on failure. The payoff: once a value has type `Email`, "this is a well-formed email" is a fact the type system guarantees — you don't re-check it downstream.

```typescript
const e: Email = asEmail("ada@example.com"); // ok
const bad: Email = "ada@example.com";        // compile error — raw string isn't an Email
```

When you read a value back out of an evaluated document, a branded field already has its brand type — it flows through without re-checking. And `evaluateRoot` checks every format as part of whole-document evaluation, so a brand and an evaluation agree.

### Supported formats

The runtime ships RFC-accurate checks for:

`date` · `time` · `date-time` · `duration` · `email` · `idn-email` · `hostname` · `idn-hostname` · `ipv4` · `ipv6` · `uuid` · `uri` · `iri` · `uri-reference` · `iri-reference` · `uri-template` · `json-pointer` · `relative-json-pointer` · `regex`

The internationalised forms (`idn-*`, `iri-*`) use proper IDNA processing; `uuid`, `ipv6` and the URI family follow their RFCs rather than a loose regex.

## Numbers

A `number` or `integer` reads as a TypeScript `number` by default. Two things make numeric handling exact rather than "good enough":

### Exact evaluation

Numeric keywords — `minimum`, `maximum`, `exclusiveMinimum/Maximum`, `multipleOf` — are evaluated against the number's **source text** using big-integer arithmetic (a decimal is decomposed into sign, integer mantissa, and base-10 exponent), not by comparing IEEE-754 doubles. So the cases where floating point lies just work:

```typescript
evaluateRoot({ score: 0.3 });  // multipleOf 0.5 -> false  (0.3 is not a multiple of 0.5)
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

### Arbitrary precision

The declared type for an ordinary `number` is `number`, but the evaluator also accepts `lossless-json` numbers. If a document carries decimals or integers beyond double precision and you need evaluation to be exact on the original digits, parse it with `lossless-json` (which preserves the source text) rather than `JSON.parse` (which rounds to a double first) — the evaluator reads the exact text either way. (A first-class arbitrary-precision *value* type, via a pluggable big-number adapter, is a planned enhancement; today the type is `number` and the precision guarantee is on *evaluation*.)

## Dates, times and durations

`date`, `time` and `date-time` are validated to **RFC 3339** (calendar-valid dates, leap-second `:60`, offsets, fractional seconds, `T`/`Z` casing). `duration` follows the **RFC 3339 ABNF** grammar, which is *stricter* than ISO 8601 / `Temporal.Duration` — units must be contiguous and descending and there are no fractions (`P1Y2D` and `PT1H2S` are invalid).

A formatted date/time field is a **branded string** — `Brand<string, "date-time">` — built and checked with its `as*` factory:

```typescript
const when = asWhen("2026-06-26T10:00:00Z"); // When = Brand<string, "date-time">
```

To work with it as a real instant or calendar value, convert the branded string at the read site:

```typescript
new Date(account.created);                       // a JS Date
Temporal.Instant.from(account.created);          // a Temporal instant (if you use Temporal)
Temporal.PlainDate.from(account.day);            // a calendar date
```

> **Planned:** generated accessors that return `Temporal` values directly (`PlainDate` / `ZonedDateTime` / `Duration`) via a pluggable adapter, so you don't convert by hand. Today the value is the branded, format-checked string and you convert it yourself.

## See also

- [examples/019-formats](./examples/019-formats/) — branded formats, runnable.
- [examples/002-validation](./examples/002-validation/) — numeric constraints in context.
- [getting-started](./getting-started.md) — the `lossless-json` / `@js-temporal/polyfill` runtime dependencies.
