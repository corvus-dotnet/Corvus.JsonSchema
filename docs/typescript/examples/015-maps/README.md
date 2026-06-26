# TypeScript Patterns - Maps

This recipe shows an open map — `additionalProperties` with a value schema — generating an index-signature type.

## The Pattern

An object whose keys aren't known ahead of time, but whose *values* share a schema, is a map. `additionalProperties: { "type": "number" }` generates `interface Scores { readonly [key: string]: number }`. A map is a plain object — read it by key, iterate with `Object.entries`, and serialise it directly (there is no `build*` for an open map); `evaluateRoot` checks every value against the value schema.

## The Schema

File: [`scores.json`](./scores.json)

```json
{ "title": "Scores", "type": "object", "additionalProperties": { "type": "number", "minimum": 0 } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const map: Scores = { ada: 9.5, alan: 8 };
const bytes = new TextEncoder().encode(JSON.stringify(map));
scores.ada;                              // typed number
for (const [name, score] of Object.entries(scores)) { /* … */ }
evaluateRoot({ ada: -1 });               // false — minimum 0 on the values
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/015-maps/demo.js`.

## Related Patterns

- [004-open-and-closed](../004-open-and-closed/) — `unevaluatedProperties` for closed objects
- [001-data-object](../001-data-object/) — fixed, named properties

## Frequently Asked Questions

### Can I mix named properties and a map?

Yes — declare the known keys in `properties` and type the rest with `additionalProperties` (or restrict which extra keys are allowed by name with `patternProperties`). The generated interface then has the named members plus an index signature for the open ones.
