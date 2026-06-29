# TypeScript Patterns - Conditional Schemas

This recipe shows `if`/`then`/`else` — applying constraints conditionally on a value's contents.

## The Pattern

`if`/`then`/`else` makes a constraint depend on the data: when the `if` subschema matches, `then` must also hold (otherwise `else`). Here, when `method` is `"card"`, `cardNumber` becomes required. This is a *constraint*, not a shape change — the generated `interface` keeps `cardNumber` optional, and `Payment.evaluate` enforces the conditional requirement.

## The Schema

File: [`payment.json`](./payment.json)

```json
{ "title": "Payment", "type": "object", "required": ["method"],
  "properties": { "method": { "enum": ["card", "cash"] }, "cardNumber": { "type": "string" } },
  "if": { "properties": { "method": { "const": "card" } } },
  "then": { "required": ["cardNumber"] } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
Payment.evaluate({ method: "cash" });                            // true
Payment.evaluate({ method: "card", cardNumber: "4111 …" });      // true
Payment.evaluate({ method: "card" });                            // false — then-clause requires cardNumber
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/017-conditional/demo.js`.

## Related Patterns

- [002-validation](../002-validation/) — unconditional constraints
- [012-discriminated-unions](../012-discriminated-unions/) — when the branches differ in *shape*, prefer `oneOf`

## Frequently Asked Questions

### Why isn't the conditional reflected in the type?

TypeScript can't express "if method is card then cardNumber is required" as a single object type without splitting it into a union. The generator keeps the honest base shape (`cardNumber?`) and enforces the rule in `Payment.evaluate`. If you want the *type* to capture the two cases, model them as a `oneOf` (a discriminated union) instead.
