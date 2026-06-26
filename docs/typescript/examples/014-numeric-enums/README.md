# TypeScript Patterns - Numeric Enumerations

This recipe shows a numeric `enum` generating a numeric-literal union.

## The Pattern

A numeric `enum` becomes `type Status = 200 | 404 | 500` — a union of numeric literals, checkable and completable, with `evaluateRoot` rejecting any other number.

## The Schema

File: [`response.json`](./response.json)

```json
{ "title": "Response", "type": "object", "required": ["status"],
  "properties": { "status": { "enum": [200, 404, 500] } } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
r.status === 200;                       // narrowed to the literal union
evaluateRoot({ status: 418 });          // false — not 200 | 404 | 500
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/014-numeric-enums/demo.js`.

## Related Patterns

- [013-string-enums](../013-string-enums/) — string `enum`
- [011-unions](../011-unions/) — `oneOf` for object branches

## Frequently Asked Questions

### Are enum values compared exactly?

Yes — numeric membership is compared by mathematical value against the schema's literals, so there is no floating-point surprise even for large or fractional members.
