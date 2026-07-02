# TypeScript Patterns - Default Values

This recipe shows the `default` annotation and the two ways the generated TypeScript surfaces it.

## The Pattern

`default` documents a fallback for a property and, in practice, makes that property optional. The generated `interface` marks it `?:`, so a value may omit it and still be valid. The parsed value is never mutated. An omitted property still reads `undefined` on the raw value. The generator gives you two ways to work with the documented defaults:

- **`{Type}.defaults`**: a readonly object literal of this type's direct property defaults (`as const`, so each value keeps its literal type). Read one default without hardcoding it: `s.theme ?? Settings.defaults.theme`.
- **`{Type}.withDefaults(value)`**: returns a copy of `value` with every absent default filled. Present properties are left as-is, and nested object types are recursed through their own `withDefaults`.

`defaults` holds only the type's own direct properties that declare a `default`; a nested object type gets its own `defaults`, so the tree is not flattened. It is emitted only for types that have at least one direct default.

## The Schema

File: [`settings.json`](./settings.json)

```json
{ "title": "Settings", "type": "object", "additionalProperties": false,
  "properties": { "theme": { "type": "string", "default": "light" }, "fontSize": { "type": "integer", "default": 14 } } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
Settings.defaults;            // { readonly theme: "light"; readonly fontSize: 14 }
Settings.defaults.theme;      // "light"

const s = Settings.parse(Settings.build({}));
s.theme ?? Settings.defaults.theme;   // read one documented default
s.fontSize ?? Settings.defaults.fontSize;

const filled = Settings.withDefaults(s);   // { theme: "light", fontSize: 14 }
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/018-defaults/demo.js`.

## Related Patterns

- [001-data-object](../001-data-object/): required vs optional properties
- [016-mutation](../016-mutation/): `build` and `produce` over document bytes

## Frequently Asked Questions

### `defaults` or `withDefaults`?

Use `defaults` to read a single documented fallback at the point you need it (`s.theme ?? Settings.defaults.theme`). Use `withDefaults` to get a copy with every absent default applied in one call, which is convenient before handing the value to code that expects the defaulted shape.

### Does either one change the parsed value?

No. `defaults` is a static literal, and `withDefaults` returns a shallow copy. The original value is untouched, so an omitted property still reads `undefined` on it.
