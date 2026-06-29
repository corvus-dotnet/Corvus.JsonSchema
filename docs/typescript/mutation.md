# Mutation

Parsed values are `readonly`, so you never change data in place. Instead the generator emits three ways to produce a **new** document, all over canonical UTF-8 JSON bytes (`Uint8Array`) — the wire and persistence shape.

## `build` — construct from values

`build{Type}(props)` serialises a fresh document:

```typescript
const bytes = Person.build({ familyName: "Brontë", givenName: "Anne", birthDate: BirthDate.from("1820-01-17") });
```

It is a single encode of the value you pass — at the floor of what serialisation can cost. `build` preserves the key order of the object you pass.

## `buildCanonical` — deterministic output

When you need byte-for-byte determinism — content-addressing, hashing/digests, signatures, cache keys, golden-file tests — use `buildCanonical{Type}(props)`. It emits **RFC 8785 (JCS)** canonical JSON: object keys recursively sorted by UTF-16 code unit, ECMAScript number forms, minimal string escaping (the same canonicalisation as the C# `JsonCanonicalizer`).

```typescript
const a = Doc.buildCanonical({ zeta: "z", alpha: 1, nested: { y: "Y", x: "X" } });
const b = Doc.buildCanonical({ nested: { x: "X", y: "Y" }, alpha: 1, zeta: "z" });
// a and b are byte-identical: {"alpha":1,"nested":{"x":"X","y":"Y"},"zeta":"z"}
```

It is a **separate** method from `build` so the fast path stays at the native floor — pay the sort only when you want determinism. (The runtime also exports the underlying `canonicalize(value)` / `canonicalJson(value)` for canonicalising a plain value directly.)

## `patch` — change only what you name

`patch{Type}(source, changes, removals?)` is the leanest update. Name the top-level fields to change, and optionally those to remove:

```typescript
Document.patch(bytes, { version: 2 });            // rewrite only "version"
Document.patch(bytes, {}, ["owner"]);             // remove the optional "owner"
```

Only the named member spans are rewritten; every other byte of `source` is copied through verbatim. The document is not parsed and the unchanged part is not re-serialised, so for a large document this avoids the cost of the usual read-modify-write cycle: parsing to an object, mutating it, and stringifying the whole thing again.

`removals` is typed to the optional properties, so you can't remove a required field.

## `produce` — an immer-style recipe

`produce{Type}(source, recipe)` gives a typed, mutable `Draft<T>` for nested and array edits:

```typescript
const next = Document.produce(bytes, (d) => {
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
