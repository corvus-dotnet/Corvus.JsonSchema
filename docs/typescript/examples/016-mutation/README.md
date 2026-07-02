# TypeScript Patterns - Mutation

This recipe covers the mutation surface in depth (`build`, `patch` and `produce`) over canonical UTF-8 JSON bytes.

## The Pattern

Parsed values are `readonly`, so you never mutate data in place. Instead the generator emits three ways to produce a *new* document, all operating on bytes:

- **`{Type}.build(props)`**: construct a fresh document from values.
- **`{Type}.patch(source, changes, removals?)`**: the leanest update. Name the top-level fields to change (and any to remove); only those member spans are rewritten, the rest of the bytes are copied through verbatim, with no parse and no re-serialise of the unchanged part.
- **`{Type}.produce(source, recipe)`**: an immer-style recipe over a typed, mutable `Draft<T>` for nested and array edits; the recorded change-set lowers to the same byte-level patch.

## The Schema

File: [`document.json`](./document.json)

```json
{ "title": "Document", "type": "object", "required": ["title"],
  "properties": {
    "title": { "type": "string" },
    "owner": { "type": "object", "properties": { "name": { "type": "string" }, "email": { "type": "string" } } },
    "tags": { "type": "array", "items": { "type": "string" } },
    "version": { "type": "integer" } } }
```

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const bytes = Document.build({ title: "Draft", owner: { name: "Ada", email: "ada@x.com" }, tags: ["wip"], version: 1 });

Document.patch(bytes, { version: 2 });           // only "version" is rewritten
Document.patch(bytes, {}, ["version"]);          // remove an optional field

Document.produce(bytes, (d) => {
  d.title = "Final";
  d.owner!.name = "Ada Lovelace";               // nested edit
  d.tags!.push("published");                    // array append
});
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/016-mutation/demo.js`.

## Related Patterns

- [001-data-object](../001-data-object/): the same trio on a flat object
- [007-arrays](../007-arrays/): `produce` with array `push`

## Frequently Asked Questions

### When should I use `patch` vs `produce`?

`patch` is the fast path for "set/remove these top-level fields". It touches only the named member spans. `produce` is for nested or conditional edits where a recipe reads more naturally; it records the changes and lowers them to the same kind of byte edit. Both leave the source document untouched and return new bytes.

### Why bytes rather than an object?

The wire/persistence shape *is* bytes. Working at the byte level lets an update splice only the changed spans and copy the rest verbatim, which is dramatically cheaper than parse → mutate object → re-serialise for large documents.
