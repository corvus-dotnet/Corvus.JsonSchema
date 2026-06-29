# TypeScript Patterns - Open and Closed Objects

This recipe shows the difference between an **open** object (unknown properties allowed) and a **closed** one (`unevaluatedProperties: false`), and how each is generated.

## The Pattern

By default a JSON Schema object is **open**: properties beyond those declared are allowed and ignored. Adding `unevaluatedProperties: false` makes it **closed** — any property not accounted for by the schema is rejected. The generated `interface` carries the declared properties either way; the open/closed distinction is enforced by `StrictPoint.evaluate`. (A closed type has no index signature, so it also reads as exact in TypeScript.)

## The Schema

File: [`point.json`](./point.json)

```json
{ "title": "StrictPoint", "type": "object", "required": ["x", "y"],
  "properties": { "x": { "type": "number" }, "y": { "type": "number" } },
  "unevaluatedProperties": false }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
StrictPoint.evaluate({ x: 1, y: 2 });        // true
StrictPoint.evaluate({ x: 1, y: 2, z: 3 });  // false — z is an unevaluated (unknown) property
StrictPoint.evaluate({ x: 1 });              // false — y is required
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/004-open-and-closed/demo.js`.

## Related Patterns

- [001-data-object](../001-data-object/) — the default (open) object
- [015-maps](../015-maps/) — `additionalProperties` typing the *unknown* keys

## Frequently Asked Questions

### What is the difference between `additionalProperties` and `unevaluatedProperties`?

`additionalProperties` constrains keys not named in *this* schema's `properties`. `unevaluatedProperties` constrains keys not accounted for by *any* applicable subschema, including those pulled in by `allOf`/`if`/`$ref` — so it's the right tool for "closed, even across composition". Setting either to `false` rejects unknown keys; setting it to a schema types them (see 015-maps).
