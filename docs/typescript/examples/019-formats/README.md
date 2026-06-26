# TypeScript Patterns - Format Validation

This recipe shows `format` keywords generating **branded** types with validating factories.

## The Pattern

A `format` (e.g. `uuid`, `uri`, `date-time`) generates a branded type — `type Id = Brand<string, "uuid">` — and a factory `asId(value): Id` that checks the format and throws `FormatError` on failure. The brand means a raw `string` is *not* assignable where an `Id` is expected: you must go through the factory, so a value's format is guaranteed by its type. `evaluateRoot` also checks formats as part of whole-document evaluation.

## The Schema

File: [`account.json`](./account.json)

```json
{ "title": "Account", "type": "object", "required": ["id"],
  "properties": { "id": { "type": "string", "format": "uuid" }, "website": { "type": "string", "format": "uri" }, "created": { "type": "string", "format": "date-time" } } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
buildAccount({
  id: asId("6f9619ff-8b86-d011-b42d-00cf4fc964ff"),
  website: asWebsite("https://example.com"),
  created: asCreated("2026-06-26T10:00:00Z"),
});
asId("nope"); // throws FormatError("uuid")
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/019-formats/demo.js`.

## Related Patterns

- [001-data-object](../001-data-object/) — a `date` brand in context
- [002-validation](../002-validation/) — `format` alongside other constraints

## Frequently Asked Questions

### Which formats are supported?

The RFC-accurate set from the JSON Schema test suite — `date`/`time`/`date-time`/`duration`, `email`/`idn-email`, `hostname`/`idn-hostname`, `ipv4`/`ipv6`, `uri`/`iri` and relatives, `uuid`, `json-pointer`, `regex`, and more. The runtime ships the checks; `asX` factories are generated for each formatted field.

### Do I have to use the factory?

To assign into a branded field, yes — that's the point: the brand makes "this string is a valid uuid" a type-level fact. When you already have a value of the branded type (e.g. read back from a validated document), it flows through without re-checking.
