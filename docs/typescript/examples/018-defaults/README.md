# TypeScript Patterns - Default Values

This recipe shows the `default` annotation and how it surfaces in the generated TypeScript.

## The Pattern

`default` documents a fallback for a property and, in practice, makes that property optional. The generated `interface` marks it `?:` — a value may omit it and still be valid. The default value is **not** currently applied to the parsed value: an omitted property reads as `undefined`, so you supply the default at the read site (`value.theme ?? "light"`).

## The Schema

File: [`settings.json`](./settings.json)

```json
{ "title": "Settings", "type": "object",
  "properties": { "theme": { "type": "string", "default": "light" }, "fontSize": { "type": "integer", "default": 14 } } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
evaluateRoot(JSON.parse("{}"));   // true — both properties are optional
const s = JSON.parse("{}") as Settings;
s.theme ?? "light";               // apply the documented default at the read site
s.fontSize ?? 14;
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/018-defaults/demo.js`.

## Related Patterns

- [001-data-object](../001-data-object/) — required vs optional properties

## Frequently Asked Questions

### Does the generator apply defaults for me?

Not yet — `default` is treated as an annotation that makes the property optional, and you apply the value with `??` where you read it. Automatic default application (filling omitted properties on read or build) is a planned enhancement.
