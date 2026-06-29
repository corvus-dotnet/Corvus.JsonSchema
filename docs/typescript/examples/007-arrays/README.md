# TypeScript Patterns - Strongly Typed Arrays

This recipe shows a homogeneous array of objects: `items` types every element, and the array reads as a `readonly T[]`.

## The Pattern

An `array` with an `items` schema generates `readonly T[]`, where `T` is the element type (here a generated `LineItem` interface). The array is read with ordinary indexing and iteration, evaluated as a whole (including `minItems`), and appended to with `produce`.

## The Schema

File: [`cart.json`](./cart.json)

```json
{ "title": "Cart", "type": "object", "required": ["items"],
  "properties": { "items": { "type": "array", "minItems": 1,
      "items": { "title": "LineItem", "type": "object", "required": ["sku", "qty"],
        "properties": { "sku": { "type": "string" }, "qty": { "type": "integer", "minimum": 1 } } } } } }
```

The generated `interface Cart` has `items: readonly LineItem[]`.

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const cart = JSON.parse(new TextDecoder().decode(bytes)) as Cart;
cart.items[0].sku;                              // indexed, typed as LineItem
cart.items.reduce((n, i) => n + i.qty, 0);      // ordinary array methods
Cart.evaluate({ items: [] });                    // false — minItems 1

// append an element; produce records the push and the array bytes are spliced.
const more = Cart.produce(bytes, (d) => { d.items.push({ sku: "C3", qty: 5 }); });
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/007-arrays/demo.js`.

## Related Patterns

- [008-nested-arrays](../008-nested-arrays/) — arrays of arrays
- [009-tuples](../009-tuples/) — fixed-length positional arrays
- [016-mutation](../016-mutation/) — `produce`/`patch` on arrays in depth

## Frequently Asked Questions

### Are the arrays mutable?

The read surface is `readonly` (you can't accidentally mutate parsed data in place). To produce a changed copy, use `{Type}.produce` with a recipe — `push`, index assignment and splice are recorded and lowered to a byte-level edit, leaving the original untouched.
