# TypeScript Patterns - References

This recipe shows how a schema reuses a common type with `$ref` and `$defs`, and how that becomes one shared `interface` with its own generated API.

## The Pattern

A subschema defined under `$defs` and referenced with `$ref` is generated **once**, as a named `interface`, and every reference to it resolves to that same type. Here `shipTo` and `billTo` both `$ref` `#/$defs/address`, so both properties are typed `Address` — define an address value once and use it for either.

A referenced type is first-class: `Address` gets its own `Address.evaluate` / `Address.build` / `Address.patch` / `Address.produce` alongside `Order`'s, so you can construct and evaluate it independently.

## The Schema

File: [`order.json`](./order.json)

```json
{
  "title": "Order",
  "type": "object",
  "$defs": {
    "address": {
      "title": "Address",
      "type": "object",
      "required": ["line1", "city", "postcode"],
      "properties": {
        "line1": { "type": "string" },
        "city": { "type": "string" },
        "postcode": { "type": "string" }
      }
    }
  },
  "required": ["id", "shipTo"],
  "properties": {
    "id": { "type": "string" },
    "shipTo": { "$ref": "#/$defs/address" },
    "billTo": { "$ref": "#/$defs/address" }
  }
}
```

The generated types are `interface Order { id: string; shipTo: Address; billTo?: Address }` and `interface Address { line1: string; city: string; postcode: string }`.

## Generated Code Usage

[Example code](./demo.ts)

### Define a referenced value once, reuse it

```typescript
const home: Address = { line1: "1 Mill Rd", city: "Cambridge", postcode: "CB1 2AB" };
const bytes = Order.build({ id: "ord-1", shipTo: home, billTo: home });
```

### Read through the reference

```typescript
const order = JSON.parse(new TextDecoder().decode(bytes)) as Order;
order.shipTo.city; // "Cambridge" — shipTo is an Address
```

### Patch a referenced sub-object

```typescript
const moved = Order.patch(bytes, {
  shipTo: { line1: "5 King's Parade", city: "Cambridge", postcode: "CB2 1ST" },
});
// only shipTo is rewritten; billTo (and everything else) is copied through
```

## Running the Example

From `docs/typescript/examples/` (run `npm install` once):

```bash
npm run build
node dist/003-references/demo.js
```

## Related Patterns

- [001-data-object](../001-data-object/) — a single object
- [005-extending](../005-extending/) — composing a base type with `allOf`

## Frequently Asked Questions

### Does each `$ref` generate a copy of the type?

No. The referenced subschema is generated once and every `$ref` to it resolves to the same `interface`. Structurally identical subschemas are also de-duplicated to a single type, so the output stays small even when a shape is referenced many times.

### What about references across files, or `$dynamicRef`?

The engine resolves `$ref`, `$dynamicRef`/`$recursiveRef`, anchors and remote documents during generation, so by the time the TypeScript is emitted the reference graph is already a plain set of named types — there is no runtime resolution. (Cross-file references resolve through the document loader you generate against.)
