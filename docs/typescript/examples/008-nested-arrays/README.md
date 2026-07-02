# TypeScript Patterns - Arrays of Higher Rank

This recipe shows nested arrays (an array whose `items` are themselves arrays) generating a matrix type.

## The Pattern

`items` can itself be an `array`, recursively. A rank-2 array generates `readonly (readonly number[])[]`; the nesting continues for rank-N (tensors). Element access is ordinary double-indexing.

## The Schema

File: [`grid.json`](./grid.json)

```json
{ "title": "Grid", "type": "object", "required": ["cells"],
  "properties": { "cells": { "type": "array", "items": { "type": "array", "items": { "type": "number" } } } } }
```

The generated `interface Grid` has `cells: readonly (readonly number[])[]`.

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const grid = Grid.parse(bytes);
grid.cells[1][2]; // 6 — typed at every level
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/008-nested-arrays/demo.js`.

## Related Patterns

- [007-arrays](../007-arrays/): a single-rank array
- [009-tuples](../009-tuples/): fixed-length arrays (a `Vec3` is `readonly [number, number, number]`)
