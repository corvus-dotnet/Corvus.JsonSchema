# Mutation

Parsed values are `readonly`, so you never change data in place. Instead the generator emits three ways to produce a **new** document, all over canonical UTF-8 JSON bytes (`Uint8Array`) — the wire and persistence shape.

## `build` — construct from values

`build{Type}(props)` serialises a fresh document:

```typescript
const bytes = buildPerson({ familyName: "Brontë", givenName: "Anne", birthDate: asBirthDate("1820-01-17") });
```

It is a single encode of the value you pass — at the floor of what serialisation can cost.

## `patch` — change only what you name

`patch{Type}(source, changes, removals?)` is the leanest update. Name the top-level fields to change, and optionally those to remove:

```typescript
patchDocument(bytes, { version: 2 });            // rewrite only "version"
patchDocument(bytes, {}, ["owner"]);             // remove the optional "owner"
```

Only the named member spans are rewritten; every other byte of `source` is copied through verbatim. There is no parse of the document and no re-serialisation of the unchanged part — for a large document this is dramatically cheaper than the read-modify-write cycle of parsing to an object, mutating, and stringifying.

`removals` is typed to the optional properties, so you can't remove a required field.

## `produce` — an immer-style recipe

`produce{Type}(source, recipe)` gives a typed, mutable `Draft<T>` for nested and array edits:

```typescript
const next = produceDocument(bytes, (d) => {
  d.title = "Final";
  d.owner!.name = "Ada Lovelace";   // nested
  d.tags!.push("published");        // array append
});
```

The recipe records the changes you make to the draft and lowers them to the same kind of byte-level edit as `patch`. The original `bytes` is untouched; `produce` returns new bytes. `Draft<T>` is `T` made deeply mutable (the `readonly` modifiers removed), so edits type-check against the real shape.

## How it works — splice, don't rebuild

All three avoid rebuilding the document:

- unchanged members stay as their original byte spans and are copied through;
- only changed values are re-encoded and spliced in;
- a trailing array `push` inserts the new element's bytes before the closing `]` without decoding the rest of the array.

This is why the byte API beats parse-mutate-stringify for the common "edit a few fields of a large document" case: the cost is proportional to what changed, not to the size of the document.

## Choosing between them

- **`build`** — you have the values and want a document.
- **`patch`** — set or remove a known set of top-level fields. The fast path.
- **`produce`** — nested edits, array edits, or conditional logic that reads more naturally as a recipe.

All three return new bytes and leave their input untouched, so they compose freely and are safe to use on shared, immutable documents.

## See also

- [reading-and-validating](./reading-and-validating.md) — the read surface these produce.
- [examples/016-mutation](./examples/016-mutation/) — a runnable walkthrough.
