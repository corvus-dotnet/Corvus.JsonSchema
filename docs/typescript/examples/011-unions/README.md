# TypeScript Patterns - Unions

This recipe shows how `oneOf` becomes a TypeScript **union** with per-branch type guards and an exhaustive `{Union}.match` function — the idiomatic way to handle "one of several shapes".

## The Pattern

A `oneOf` of object schemas generates a union type alias (`type Shape = Circle | Rectangle`) plus:

- a **type guard** per branch (`Circle.is(value): value is Circle`) that narrows in an `if`;
- an exhaustive **`Shape.match(value, cases)`** that dispatches to a handler per branch and returns a result — the compiler requires a case for *every* member, so adding a branch to the schema turns missing handlers into compile errors.

When each branch carries a discriminant property (here `kind: "circle"` / `"rectangle"` via `const`), the guards key off it; the same shape with no discriminant still works, distinguished structurally by the evaluator.

## The Schema

File: [`shape.json`](./shape.json)

```json
{
  "title": "Shape",
  "oneOf": [
    { "title": "Circle", "type": "object", "required": ["kind", "radius"],
      "properties": { "kind": { "const": "circle" }, "radius": { "type": "number" } } },
    { "title": "Rectangle", "type": "object", "required": ["kind", "width", "height"],
      "properties": { "kind": { "const": "rectangle" }, "width": { "type": "number" }, "height": { "type": "number" } } }
  ]
}
```

## Generated Code Usage

[Example code](./demo.ts)

### Match exhaustively

```typescript
const area = (s: Shape) =>
  Shape.match(s, {
    circle: (c) => Math.PI * c.radius * c.radius,
    rectangle: (r) => r.width * r.height,
  });
```

### Narrow with a guard

```typescript
if (Circle.is(shape)) {
  shape.radius; // here `shape` is typed as Circle
}
```

### Evaluate

```typescript
Shape.evaluate(shape); // true if the value matches exactly one branch
```

## Running the Example

From `docs/typescript/examples/` (run `npm install` once):

```bash
npm run build
node dist/011-unions/demo.js
```

## Related Patterns

- [012-discriminated-unions](../012-discriminated-unions/) — a shared discriminator property across branches
- [014-numeric-enums](../014-numeric-enums/) — `oneOf` of `const` values

## Frequently Asked Questions

### How is `oneOf` different from `anyOf` here?

Both generate a union type and `{Union}.match`. The difference is in evaluation: `oneOf` requires the value to match *exactly one* branch (matching two is invalid), while `anyOf` accepts one *or more*. The TypeScript surface is the same; `Shape.evaluate` enforces the cardinality.

### Why is `Shape.match` better than a manual `switch`?

It is exhaustive by construction: the `cases` object must have a handler for every union member, so if you later add a `Triangle` branch to the schema, the regenerated `Shape.match` won't type-check until you handle it — you can't silently forget a case.
