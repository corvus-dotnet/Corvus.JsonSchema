# TypeScript Patterns - Format Validation

This recipe shows `format` keywords generating **branded** types with validating factories, and the accessors that convert a branded value into a strong runtime type.

## The Pattern

A `format` (e.g. `uuid`, `uri`, `date-time`) generates a branded type (`type Id = Brand<string, "uuid">`) and a factory `Id.from(value): Id` that checks the format and throws `FormatError` on failure. The brand means a raw `string` is *not* assignable where an `Id` is expected. You go through the factory, so a value's format is guaranteed by its type. `Account.evaluate` also checks formats as part of whole-document evaluation.

A branded value is still a `string` (or `number`) at runtime, so it serializes and prints as-is. When you need a *strong* runtime value, a calendar or instant type for a date, or the exact digits of an arbitrary-precision number, the companion carries a conversion accessor (see [Getting strong values out](#getting-strong-values-out)).

## The Schema

File: [`account.json`](./account.json)

```json
{ "title": "Account", "type": "object", "required": ["id"],
  "properties": { "id": { "type": "string", "format": "uuid" }, "website": { "type": "string", "format": "uri" }, "created": { "type": "string", "format": "date-time" } } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
Account.build({
  id: Id.from("6f9619ff-8b86-d011-b42d-00cf4fc964ff"),
  website: Website.from("https://example.com"),
  created: Created.from("2026-06-26T10:00:00Z"),
});
Id.from("nope"); // throws FormatError("uuid")

// Convert the branded date-time back out to a strong Temporal value:
const when = Created.toTemporal(Created.from("2026-06-26T10:00:00Z")); // Temporal.Instant
```

## Getting strong values out

`.from` brings a value *in* (validate, then brand). The companion also carries accessors that take a branded value back *out* to a strong runtime type. They are pure parse helpers. They never re-validate (the brand already proves the format) and never mutate the value.

**Temporal formats** (`date`, `date-time`, `time`, `duration`) get `{Type}.toTemporal(value)`, returning the matching `Temporal` value type (the runtime provides `Temporal`, so no extra setup is needed):

| Format | `toTemporal` returns |
|---|---|
| `date` | `Temporal.PlainDate` |
| `date-time` | `Temporal.Instant` |
| `time` | `Temporal.PlainTime` |
| `duration` | `Temporal.Duration` |

For this recipe's `created` (`date-time`), that is `Created.toTemporal(value): Temporal.Instant`.

**Arbitrary-precision numeric formats** (`decimal`, or a numeric field whose value can exceed IEEE-754 double precision) get `{Type}.toExact(value): string`, returning the exact decimal digits as a string. That is the zero-dependency big-number seam. Pass the string to whatever big-decimal library you use, with no precision lost through a JavaScript `number`.

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/019-formats/demo.js`.

## Related Patterns

- [001-data-object](../001-data-object/): a `date` brand in context
- [002-validation](../002-validation/): `format` alongside other constraints

## Frequently Asked Questions

### Which formats are supported?

The RFC-accurate set from the JSON Schema test suite: `date`/`time`/`date-time`/`duration`, `email`/`idn-email`, `hostname`/`idn-hostname`, `ipv4`/`ipv6`, `uri`/`iri` and relatives, `uuid`, `json-pointer`, `regex`, and more. The runtime ships the checks; `{Type}.from` factories are generated for each formatted field.

### Do I have to use the factory?

To assign into a branded field, yes. That is the point. The brand makes "this string is a valid uuid" a type-level fact. When you already have a value of the branded type (for example, read back from a validated document), it flows through without re-checking.

### How do I get a date or a big number out of a branded value?

Use the conversion accessor on the companion. `{Type}.toTemporal(value)` returns the matching `Temporal` type for the temporal formats (a `Temporal` value, not a legacy `Date`), and `{Type}.toExact(value)` returns the exact decimal digits as a string for arbitrary-precision numeric formats. See [Getting strong values out](#getting-strong-values-out).