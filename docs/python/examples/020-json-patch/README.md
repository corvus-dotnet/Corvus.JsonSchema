# Python patterns, JSON Merge Patch

This recipe shows applying and building [RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396)
documents against a generated type's bytes.

> RFC 6902 JSON Patch (`apply_patch` / `create_patch`) is not yet implemented in the Python runtime; those
> functions raise `NotImplementedError`. This recipe uses the RFC 7396 merge-patch wrappers, which are fully
> supported.

## The pattern

`patch_<t>` edits a document by naming the fields you want to change. **JSON Merge Patch** is the simpler,
interchange-friendly form for partial updates: a JSON document that overlays the target, where a member set to
`null` deletes that key and nested objects merge recursively.

For every buildable type the generator emits the merge-patch pair over the document bytes (or a parsed value):

- `apply_merge_patch_contact(doc, merge_patch)`. Apply an RFC 7396 merge patch, returning canonical bytes
  (RFC 8785). A member set to `None` deletes that key; nested objects merge recursively.
- `create_merge_patch_contact(source, target)`. Compute the RFC 7396 merge patch between two documents.

Both accept either a typed value or the wire bytes. Alongside them the type carries the usual
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
def apply_merge_patch_contact(doc: Contact | bytes, merge_patch: object) -> bytes: ...
def create_merge_patch_contact(source: Contact | bytes, target: Contact | bytes) -> object: ...
```

## The demo

See [`demo.py`](./demo.py). It builds a contact, applies a merge patch (replace one member, delete a nested key
with `None`, delete an array member with `None`), diffs two documents into a merge patch, and confirms the diff
round-trips.

```
original:         {"name":"Ada Lovelace","email":"ada@example.com","phones":["+1-555-0100"],"address":{"city":"London","zip":"EC1"},"version":1}
1. applyMerge:    {"address":{"city":"London"},"email":"ada@new.example.com","name":"Ada Lovelace","version":1}
2. createMerge:   {"email": null, "name": "Ada L.", "address": {"zip": null, "city": "Oxford"}, "version": 2}
3. round-trips:   True
```

Line 1 replaces `email`, deletes `address.zip` while keeping `address.city`, and drops `phones` entirely; the
result is canonical (sorted keys). Line 2 is the merge patch that turns the original into the target: `null`
marks each deletion (`email`, `address.zip`), and applying it back to the original reproduces the canonical
target (line 3).

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
