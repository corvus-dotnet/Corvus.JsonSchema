# TypeScript Patterns - Mix-in Types

This recipe composes several independent base types into one with a multi-member `allOf`.

## The Pattern

`allOf` can reference more than one base. `Widget` mixes in `Named` (a `name`) and `Timestamped` (a `createdAt`), and adds its own `id` — the generated `interface` merges all of them, and a value must satisfy each. This is the "combine orthogonal capabilities" pattern.

## The Schema

File: [`widget.json`](./widget.json)

```json
{ "title": "Widget", "type": "object",
  "$defs": {
    "named": { "title": "Named", "type": "object", "required": ["name"], "properties": { "name": { "type": "string" } } },
    "timestamped": { "title": "Timestamped", "type": "object", "properties": { "createdAt": { "type": "string", "format": "date-time" } } } },
  "allOf": [ { "$ref": "#/$defs/named" }, { "$ref": "#/$defs/timestamped" } ],
  "properties": { "id": { "type": "string" } } }
```

The generated `interface Widget` has `name`, `createdAt?` (a `date-time` brand), and `id?`.

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const bytes = Widget.build({ name: "gauge", createdAt: CreatedAt.from("2026-06-26T10:00:00Z"), id: "w-1" });
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/010-mixins/demo.js`.

## Related Patterns

- [005-extending](../005-extending/) — one base plus an extension
- [003-references](../003-references/) — the `$ref`/`$defs` the mix-ins are referenced by
