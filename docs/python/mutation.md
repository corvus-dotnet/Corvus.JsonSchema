# Mutation

Parsed values are read as plain `TypedDict`s, so the mutation API never changes data in place. It produces a
new document over canonical UTF-8 JSON bytes, the wire and persistence shape. The generator emits `build_<t>`,
`build_canonical_<t>`, `patch_<t>`, `produce_<t>`, and, when the subtree declares defaults, `with_defaults_<t>`.

## `build_<t>`: construct from values

`build_<t>(value)` serialises a fresh document from plain values.

```python
from generated import build_person, from_birth_date

data = build_person({
    "family_name": "Bronte",
    "given_name": "Anne",
    "birth_date": from_birth_date("1820-01-17"),   # a validating branded factory for `format: date`
})
```

It is a single encode of the value you pass, at the floor of what serialisation can cost. `build_<t>` preserves
the key order of the object you pass. Numbers serialise from their exact value, so a `Decimal` keeps every
digit.

## `build_canonical_<t>`: deterministic output

When you need byte-for-byte determinism, for content-addressing, hashing, signatures, cache keys, or
golden-file tests, use `build_canonical_<t>(value)`. It emits RFC 8785 canonical JSON, with object keys
recursively sorted, the same canonicalisation as the C# `JsonCanonicalizer` and the TypeScript
`buildCanonical`.

```python
from generated import build_canonical_document

a = build_canonical_document({"id": "d", "title": "T", "version": 1})
b = build_canonical_document({"version": 1, "title": "T", "id": "d"})
# a and b are byte-identical
```

It is a separate function from `build_<t>` so the fast path stays at the native floor. Pay the sort only when
you want determinism. The runtime also exports the underlying `canonicalize(value)` for canonicalising a plain
value directly.

## `patch_<t>`: change only what you name

`patch_<t>(source, changes, removals=None, arrays=None)` is the leanest update. It is a read-modify-write at
the byte level. It splices only the changed value bytes into `source` and copies every other byte through
verbatim. The document is not parsed and the unchanged part is not re-serialised, so for a large document this
avoids the cost of the usual parse, mutate, and re-serialise cycle. The cost is proportional to what changed,
not to the size of the document.

```python
from generated import build_document, patch_document

data = build_document({"id": "doc-1", "title": "Draft", "tags": ["a", "b"]})

patch_document(data, {"title": "Final"})          # rewrite only "title"
patch_document(data, {}, ["tags"])                # remove the optional "tags"
```

- `changes` is a mapping of member name to new value. It upserts each named member.
- `removals` is a sequence of member names to delete.

### Array-element edits

The `arrays` argument applies element edits to an array-valued member without re-encoding the rest of the
array. It maps a member name to a `corvus_json_runtime.RmwArrayOps`, whose keys are `set` (replace at index),
`insert` (splice values before an index), `remove_at` (delete indices), and `append` (add to the end). A
tuple-tail `rest` block is also supported for `prefixItems` arrays.

```python
from corvus_json_runtime import RmwArrayOps
from generated import patch_document

tag_ops: RmwArrayOps = {"append": ["c"], "set": {0: "A"}}
patch_document(data, {}, None, {"tags": tag_ops})   # element 0 replaced, "c" appended
```

Only the touched element spans are rewritten. Every other element, and every other member, is copied through.
The `arrays` argument is emitted only for a type that has an array member. See
[examples/016-mutation](./examples/016-mutation/).

## `with_defaults_<t>`: fill declared defaults

`with_defaults_<t>(value)` returns a shallow copy of the value with every absent property that declares a
`default` filled in, recursing into present nested objects and arrays of objects that carry their own defaults.
It is emitted only when the subtree declares a `default`.

```python
from generated import with_defaults_document

with_defaults_document({"id": "doc-2"})           # {'id': 'doc-2', 'title': 'Untitled', 'version': 1}
```

Unlike the other three, `with_defaults_<t>` works on a parsed value and returns a parsed value, not bytes. Pass
its result to `build_<t>` when you want the filled document as bytes.

## `produce_<t>`: mutate a decoded draft

`produce_<t>(source, recipe)` decodes `source` into a mutable draft, runs `recipe` over that draft, and returns
the result as canonical bytes. The recipe mutates the draft in place, so an edit reads like ordinary Python. The
source bytes are left untouched.

```python
from generated import Document, build_document, produce_document

data = build_document({"id": "doc-3", "title": "Draft", "version": 1})


def publish(draft: Document) -> None:
    draft["title"] = "Published"
    draft["version"] = draft.get("version", 0) + 1


produce_document(data, publish)   # canonical bytes with the recipe applied
```

The recipe takes the decoded draft, typed as the model, and returns `None`. Use `produce_<t>` for a whole-value
transform that is easier to express as in-place edits than as a set of named changes. When you know exactly the
members to set or remove, `patch_<t>` is the byte-native splice and does not decode the whole document.

## How it works: splice, don't rebuild

`build_<t>` and `build_canonical_<t>` encode a whole value. `patch_<t>` avoids rebuilding.

- unchanged members stay as their original byte spans and are copied through.
- only changed values are re-encoded and spliced in.
- a trailing array `append` inserts the new element's bytes before the closing `]` without decoding the rest of
  the array.

## Choosing between them

- `build_<t>`, you have the values and want a document.
- `build_canonical_<t>`, you need deterministic, sorted-key bytes.
- `patch_<t>`, set or remove a known set of members, or edit array elements, on an existing document. The fast
  path.
- `produce_<t>`, apply a recipe of in-place edits to a decoded draft when that is clearer than a set of named
  changes.

They all return new bytes and leave their input untouched, so they are safe to use on shared, immutable
documents.

## See also

- [json-patch](./json-patch.md), RFC 6902 JSON Patch and RFC 7396 merge patch over the same bytes.
- [reading-and-validating](./reading-and-validating.md), the read surface these produce.
- [examples/016-mutation](./examples/016-mutation/), a runnable walkthrough.
