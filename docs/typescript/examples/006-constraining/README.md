# TypeScript Patterns - Constraining a Base Type

This recipe uses `allOf` not to *add* properties but to *tighten* a base type's constraints.

## The Pattern

A member of `allOf` can re-state an existing property with extra keywords; all members apply together. Here `Batch` requires `size >= 1`, and `SmallBatch` composes it with `size <= 100`, so the effective constraint is `1 <= size <= 100`. The generated shape is unchanged (`{ size }`); the *constraints* are the union of all members and are enforced by `evaluateRoot`.

## The Schema

File: [`small-batch.json`](./small-batch.json)

```json
{ "title": "SmallBatch", "type": "object",
  "$defs": { "batch": { "title": "Batch", "type": "object", "required": ["size"],
      "properties": { "size": { "type": "integer", "minimum": 1 } } } },
  "allOf": [ { "$ref": "#/$defs/batch" } ],
  "properties": { "size": { "maximum": 100 } } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
evaluateRoot({ size: 50 });   // true
evaluateRoot({ size: 200 });  // false — maximum 100 (added by SmallBatch)
evaluateRoot({ size: 0 });    // false — minimum 1 (from the base)
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/006-constraining/demo.js`.

## Related Patterns

- [005-extending](../005-extending/) — `allOf` that adds properties
- [002-validation](../002-validation/) — the constraint keywords themselves

## Frequently Asked Questions

### Why doesn't the constraint show up in the TypeScript type?

A numeric range can't be expressed in a TypeScript type, so `size` is just `number` in the interface and the `1..100` bound lives in `evaluateRoot` — the same split between *shape* and *constraint* as plain validation (002).
