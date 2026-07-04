# JSON Merge Patch

This guide covers applying and building [RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396)
documents. Where [mutation](./mutation.md) edits a document by naming the members to change, a merge patch is a
data description of a change, a value you can receive over the wire, persist, or compute as a diff between two
documents.

## The companion functions

For every buildable type the generator emits a merge-patch pair over the document bytes. Each accepts the
document as `bytes` or an already-parsed value. `apply_merge_patch_<t>` returns canonical UTF-8 JSON bytes.

| Function | Purpose |
|---|---|
| `apply_merge_patch_<t>(doc, merge_patch)` | Apply a merge patch to `doc`; returns canonical bytes. |
| `create_merge_patch_<t>(source, target)` | Compute the merge patch that turns `source` into `target`. |

The same operations are available as the generic `apply_merge_patch` and `create_merge_patch` functions,
re-exported from `corvus_json_runtime`, which take and return parsed JSON values.

## Applying a merge patch

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

The result is canonical bytes. `apply_merge_patch_<t>` does not validate the result against the schema, so when
the patch came from an untrusted source, evaluate the result with `evaluate(next_bytes)`.

## Diffing two documents

`create_merge_patch_<t>` produces the change that turns one document into another, useful for storing or
transmitting an edit, or for an audit log. It returns the merge patch as a parsed value.

```python
from generated import create_merge_patch_order

merge = create_merge_patch_order(before, after)
# apply_merge_patch_order(before, merge) reproduces after
```

## RFC 6902 JSON Patch

RFC 6902 JSON Patch, the operation-array form with `add` / `remove` / `replace` / `move` / `copy` / `test`, is
not yet implemented in the Python runtime. The `apply_patch` and `create_patch` functions and the
`JsonPatchError` class are present in `corvus_json_runtime`, but `apply_patch` and `create_patch` raise
`NotImplementedError`. Use merge patch, or the field-level [mutation](./mutation.md) API, until the operation
patch lands. This is the one place the Python engine trails the TypeScript engine, which ships both.

## See also

- [mutation](./mutation.md), field-level `build_<t>` / `patch_<t>` / `with_defaults_<t>`.
- [reading-and-validating](./reading-and-validating.md), evaluating a patched result before you trust it.
