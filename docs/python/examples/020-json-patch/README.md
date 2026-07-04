# Python patterns, JSON Patch and JSON Merge Patch

This recipe shows the two JSON patch families the generator emits over a type's bytes:
[RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902), the ordered operation array, and
[RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396), the overlay document.

## The pattern

`patch_<t>` edits a document by naming the fields you want to change. The two patch families are data
descriptions of a change, values you can receive over the wire, persist, or compute as a diff between two
documents.

- **RFC 6902 JSON Patch** is an ordered array of operations (`add` / `remove` / `replace` / `move` / `copy` /
  `test`), each naming a JSON Pointer path. It can reorder array elements, patch a single element, and guard a
  precondition with `test`.
- **RFC 7396 JSON Merge Patch** is a single overlay document, where a member set to `null` deletes that key and
  nested objects merge recursively. It is the simpler shape for a partial update, at the cost of not being able
  to set a value to `null` or to patch individual array elements.

For every buildable type the generator emits both pairs over the document bytes (or a parsed value):

- `apply_patch_contact(doc, patch)`. Apply an RFC 6902 patch, returning canonical bytes (RFC 8785). A failed
  `test` aborts the whole patch.
- `create_patch_contact(source, target)`. Compute the RFC 6902 patch between two documents, as an ops list.
- `apply_merge_patch_contact(doc, merge_patch)`. Apply an RFC 7396 merge patch, returning canonical bytes.
- `create_merge_patch_contact(source, target)`. Compute the RFC 7396 merge patch between two documents.

It also emits `produce_contact(source, recipe)`, which runs a recipe over a decoded draft. The recipe mutates
the draft in place, and produce returns the result as canonical bytes, leaving the source untouched.

Both patch pairs accept either a typed value or the wire bytes. Alongside them the type carries the usual
`build_contact` / `build_canonical_contact` / `parse_contact` / `patch_contact` surface for typed, field-level
construction and mutation.

## The schema

```json
{
  "title": "Contact",
  "type": "object",
  "required": ["name"],
  "properties": {
    "name": { "type": "string" },
    "displayName": { "type": "string" },
    "email": { "type": "string" },
    "phones": { "type": "array", "items": { "type": "string" } },
    "address": { "type": "object", "properties": { "city": { "type": "string" }, "zip": { "type": "string" } } },
    "version": { "type": "integer" }
  }
}
```

## The generated surface

```python
def apply_patch_contact(doc: Contact | bytes, patch: object) -> bytes: ...
def create_patch_contact(source: Contact | bytes, target: Contact | bytes) -> object: ...
def apply_merge_patch_contact(doc: Contact | bytes, merge_patch: object) -> bytes: ...
def create_merge_patch_contact(source: Contact | bytes, target: Contact | bytes) -> object: ...
def produce_contact(source: bytes, recipe: Callable[[Contact], None]) -> bytes: ...
```

## The demo

See [`demo.py`](./demo.py). It builds a contact, applies an RFC 6902 patch with each operation kind, diffs two
documents into an RFC 6902 patch and confirms it round-trips, applies and diffs an RFC 7396 merge patch, then
runs a `produce` recipe.

```
original:         {"name":"Ada Lovelace","email":"ada@example.com","phones":["+1-555-0100"],"address":{"city":"London","zip":"EC1"},"version":1}
1. applyPatch:    {"address":{"city":"London"},"displayName":"Ada Lovelace","email":"ada@new.example.com","name":"Ada Lovelace","phones":["+44-20-5550100","+1-555-0100"],"version":1}
2. createPatch:   [{"op": "replace", "path": "/email", "value": "ada@lovelace.example"}, {"op": "replace", "path": "/address/city", "value": "Oxford"}, {"op": "replace", "path": "/version", "value": 2}]
3. round-trips:   True
4. applyMerge:    {"address":{"city":"London"},"email":"ada@new.example.com","name":"Ada Lovelace","version":1}
5. createMerge:   {"email": "ada@lovelace.example", "address": {"city": "Oxford"}, "version": 2}
6. produce:       {"address":{"city":"London","zip":"EC1"},"displayName":"Ada L.","email":"ada@example.com","name":"Ada Lovelace","phones":["+1-555-0100"],"version":2}
```

Line 1 runs `test` (version is 1, so the patch proceeds), appends a phone, replaces `email`, copies `name` into
a new `displayName`, reorders the two phones with `move`, and removes `address.zip`; the result is canonical
(sorted keys). Line 2 diffs the original against the target into three `replace` operations, and line 3 confirms
applying that diff reproduces the canonical target. Line 4 is the merge-patch overlay: it replaces `email`,
deletes `address.zip` while keeping `address.city`, and drops `phones` entirely. Line 5 is the merge patch that
turns the original into the target. Line 6 runs a recipe over a decoded draft (bump the version, set a display
name) and returns canonical bytes.

`apply_patch_contact` and `apply_merge_patch_contact` do not validate the result against the schema, so when a
patch came from an untrusted source, evaluate the result with `evaluate` before you trust it.

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
