# Python patterns, byte-native mutation

This recipe shows the mutation API the generator emits for an object type: `build`, `patch` (member and
array-element edits), and `with_defaults`. The distinctive part is that `patch` is a read-modify-write **at the
byte level**. It splices only the changed value bytes into the source document and copies every other byte
through verbatim, without parsing and re-serialising the whole value.

## The pattern

For a schema with declared properties the generator emits, alongside the `TypedDict` surface and the validator:

- `build_document(props)` and `build_canonical_document(props)`. Plain values to compact / RFC 8785 bytes.
- `patch_document(source, changes, removals=None, arrays=None)`. A byte-native partial update.
  - `changes` upserts named members (`{"title": "Final"}`).
  - `removals` deletes named members (`["tags"]`).
  - `arrays` applies element edits to an array member (`{"tags": {"append": ["c"], "set": {0: "A"}}}`); the ops
    are a `corvus_json_runtime.RmwArrayOps` with `set` / `insert` / `remove_at` / `append`.
- `with_defaults_document(value)`. Return a shallow copy with every absent property that declares a `default`
  filled in, recursing into present nested objects and arrays-of-object that carry their own defaults.

## The schema

```json
{
  "title": "Document",
  "type": "object",
  "required": ["id"],
  "properties": {
    "id": { "type": "string" },
    "title": { "type": "string", "default": "Untitled" },
    "version": { "type": "integer", "default": 1 },
    "tags": { "type": "array", "items": { "type": "string" } }
  }
}
```

## The demo

See [`demo.py`](./demo.py).

```
1. built:         {"id":"doc-1","title":"Draft","tags":["a","b"]}
2. member patch:  {"id":"doc-1","title":"Final"}
3. array patch:   {"id":"doc-1","title":"Draft","tags":["A","b","c"]}
4. with_defaults: {'id': 'doc-2', 'title': 'Untitled', 'version': 1}
```

Line 2 rewrites only `title`'s value span and drops the `tags` member; `id` is copied through unchanged. Line 3
splices `tags` in place (element 0 replaced, `"c"` appended) while `id` and `title` are untouched.

## Running it

From `docs/python/examples`:

```bash
pwsh ./regenerate.ps1 -Check
```
