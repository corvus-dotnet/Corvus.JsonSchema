# TypeScript Patterns - Discriminated Unions

This recipe shows a tagged union (a `oneOf` whose branches share a discriminant property) and the exhaustive dispatch it generates.

## The Pattern

When every branch of a `oneOf` carries a `const` discriminant (here `type: "click" | "keypress" | "scroll"`), the generated `{Union}.match` keys off it to dispatch to a per-branch handler. `Event.match` is exhaustive. The `cases` object must have a handler for every member, so adding a branch to the schema turns a missing case into a compile error.

## The Schema

File: [`event.json`](./event.json)

```json
{ "title": "Event",
  "oneOf": [
    { "title": "Click",    "type": "object", "required": ["type", "x", "y"],   "properties": { "type": { "const": "click" },    "x": { "type": "number" }, "y": { "type": "number" } } },
    { "title": "KeyPress", "type": "object", "required": ["type", "key"],       "properties": { "type": { "const": "keypress" }, "key": { "type": "string" } } },
    { "title": "Scroll",   "type": "object", "required": ["type", "delta"],     "properties": { "type": { "const": "scroll" },   "delta": { "type": "number" } } } ] }
```

The generated `type Event = Click | KeyPress | Scroll`, with `Click.is`/`KeyPress.is`/`Scroll.is` guards and `Event.match`.

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const describe = (e: Event) =>
  Event.match(e, {
    click:    (c) => `click at ${c.x},${c.y}`,
    keyPress: (k) => `key ${k.key}`,
    scroll:   (s) => `scroll ${s.delta}`,
  });
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/012-discriminated-unions/demo.js`.

## Related Patterns

- [011-unions](../011-unions/): `oneOf` in general (and `{Union}.match`)
- [013-string-enums](../013-string-enums/): the discriminant values as a literal union

## Frequently Asked Questions

### Does the discriminant make evaluation faster?

It can short-circuit. A value with `type: "scroll"` only needs the `Scroll` branch evaluated. But the discriminant is a convenience for *your* dispatch (`{Union}.match`); `Event.evaluate` is correct with or without one, distinguishing branches structurally when there is no tag.
