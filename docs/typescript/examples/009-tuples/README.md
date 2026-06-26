# TypeScript Patterns - Tuples

This recipe shows a fixed-length positional array — `prefixItems` with `items: false` — generating a TypeScript tuple type.

## The Pattern

`prefixItems` types each position; `items: false` forbids anything beyond them, and `minItems` requires them all to be present. Together they generate a tuple — `readonly [number, number, number]` — which destructures and indexes positionally with full types.

## The Schema

File: [`point3d.json`](./point3d.json)

```json
{ "title": "Point3D", "type": "object", "required": ["coord"],
  "properties": { "coord": { "type": "array", "minItems": 3,
      "prefixItems": [ { "type": "number" }, { "type": "number" }, { "type": "number" } ], "items": false } } }
```

The generated `interface Point3D` has `coord: readonly [number, number, number]`.

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const [x, y, z] = p.coord;                  // positional, each typed number
evaluateRoot({ coord: [1, 2] });            // false — minItems 3
evaluateRoot({ coord: [1, 2, 3, 4] });      // false — items: false (no fourth element)
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/009-tuples/demo.js`.

## Related Patterns

- [007-arrays](../007-arrays/) — variable-length homogeneous arrays
- [008-nested-arrays](../008-nested-arrays/) — arrays of arrays

## Frequently Asked Questions

### Why does the tuple need `minItems` as well as `prefixItems`?

`prefixItems` only types the positions that are *present* — on its own it allows a shorter array. Adding `minItems: 3` (and `items: false` for the upper bound) is what makes the length exactly three, matching the `readonly [number, number, number]` type.
