# JSON Patch and JSON Merge Patch

This guide covers the two patch families the generator emits over a type's bytes:
[RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902), the ordered operation array, and
[RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396), the overlay document. Where
[mutation](./mutation.md) edits a document by naming the members to change, a patch is a data description of a
change, a value you can receive over the wire, persist, or compute as a diff between two documents.

## The companion functions

For every buildable type the generator emits both patch pairs over the document bytes. Each accepts the
document as `bytes` or an already-parsed value. The `apply_*` functions return canonical UTF-8 JSON bytes
(RFC 8785).

| Function | Purpose |
|---|---|
| `apply_patch_<t>(doc, patch)` | Apply an RFC 6902 operation patch to `doc`; returns canonical bytes. |
| `create_patch_<t>(source, target)` | Compute the RFC 6902 patch that turns `source` into `target`, as an ops list. |
| `apply_merge_patch_<t>(doc, merge_patch)` | Apply an RFC 7396 merge patch to `doc`; returns canonical bytes. |
| `create_merge_patch_<t>(source, target)` | Compute the RFC 7396 merge patch that turns `source` into `target`. |

The same operations are available as the generic `apply_patch`, `create_patch`, `apply_merge_patch`, and
`create_merge_patch` functions, re-exported from `corvus_json_runtime`, which take and return parsed JSON
values. A patch that cannot be applied (a failed `test`, a missing path, a bad operation) raises
`JsonPatchError`.

## RFC 6902 JSON Patch

An RFC 6902 patch is an ordered array of operations, each naming a JSON Pointer path. The operations are `add`,
`remove`, `replace`, `move`, `copy`, and `test`. Unlike a merge patch, it can reorder array elements, patch a
single element, set a member to `null`, and guard a precondition with `test`.

```python
from generated import apply_patch_order

next_bytes = apply_patch_order(data, [
    {"op": "test", "path": "/version", "value": 1},              # abort the whole patch unless version is 1
    {"op": "replace", "path": "/status", "value": "shipped"},
    {"op": "add", "path": "/tags/-", "value": "priority"},       # append to an array
    {"op": "remove", "path": "/draft"},
])
```

The operations apply in order. A failed `test` raises `JsonPatchError` and no partial change is kept. The
result is canonical bytes.

`create_patch_<t>` produces the operation array that turns one document into another, emitting `add`, `remove`,
and `replace` operations.

```python
from generated import create_patch_order

ops = create_patch_order(before, after)
# apply_patch_order(before, ops) reproduces after
```

## RFC 7396 JSON Merge Patch

A merge patch is an ordinary JSON document overlaid on the target. Each member replaces the corresponding
member, a nested object merges recursively, and a member set to `None` deletes that key. It is the natural
shape for a partial update, at the cost of not being able to set a value to `null` or to patch individual array
elements.

```python
from generated import apply_merge_patch_order

next_bytes = apply_merge_patch_order(data, {
    "status": "shipped",             # replace
    "shipping": {"tracking": "Z"},   # merge into the existing shipping object
    "draft": None,                   # delete the "draft" key
})
```

`create_merge_patch_<t>` produces the merge patch that turns one document into another, useful for storing or
transmitting an edit, or for an audit log. It returns the merge patch as a parsed value.

```python
from generated import create_merge_patch_order

merge = create_merge_patch_order(before, after)
# apply_merge_patch_order(before, merge) reproduces after
```

## Validate an untrusted result

Neither `apply_patch_<t>` nor `apply_merge_patch_<t>` validates the result against the schema, so when the
patch came from an untrusted source, evaluate the result with `evaluate(next_bytes)`.

## Choosing between them

- RFC 6902 when you need ordered operations, array-element edits, a precondition (`test`), or the ability to set
  a member to `null`.
- RFC 7396 when a plain overlay is enough, the interchange-friendly form for a partial update.
- The field-level [mutation](./mutation.md) API (`patch_<t>`) when you already know the members to change and
  want the byte-native splice.

## See also

- [mutation](./mutation.md), field-level `build_<t>` / `patch_<t>` / `with_defaults_<t>` / `produce_<t>`.
- [reading-and-validating](./reading-and-validating.md), evaluating a patched result before you trust it.
- [examples/020-json-patch](./examples/020-json-patch/), a runnable walkthrough of both patch families.
