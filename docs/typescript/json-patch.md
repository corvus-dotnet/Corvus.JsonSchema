# JSON Patch and Merge Patch

This guide covers applying and building [RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902) and [RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396) documents. Where [mutation](./mutation.md) edits a document by naming the fields to change, a JSON Patch is a *data* description of a change — a value you can receive over the wire, persist, or compute as a diff between two documents.

## The companion methods

For every buildable type the generator emits four methods over the document bytes. Each accepts the document as a `Uint8Array` (it decodes) or an already-parsed value, and the `apply*` pair returns canonical UTF-8 JSON bytes:

| Method | RFC | Purpose |
|---|---|---|
| `Type.applyPatch(doc, patch)` | 6902 | Apply a patch (an array of operations); returns canonical bytes. |
| `Type.createPatch(source, target)` | 6902 | Compute a minimal patch that turns `source` into `target`. |
| `Type.applyMergePatch(doc, mergePatch)` | 7396 | Apply a merge patch; returns canonical bytes. |
| `Type.createMergePatch(source, target)` | 7396 | Compute the merge patch between two documents. |

The same operations are available as the generic `applyPatch` / `createPatch` / `applyMergePatch` / `createMergePatch` functions — re-exported from every module — which take and return parsed JSON values. The `JsonPatch` and `JsonPatchOp` types and the `JsonPatchError` class are re-exported too.

## RFC 6902 JSON Patch

A patch is a `JsonPatch` — an array of operations, each with an `op` and a JSON Pointer `path`:

```typescript
import { Order, type JsonPatch } from "./generated.js";

const patch: JsonPatch = [
  { op: "test", path: "/version", value: 1 },              // guard
  { op: "replace", path: "/status", value: "shipped" },
  { op: "add", path: "/items/-", value: { sku: "A1", qty: 2 } }, // "-" appends
  { op: "move", from: "/tmp", path: "/note" },
  { op: "copy", from: "/billing", path: "/shipping" },
  { op: "remove", path: "/draft" },
];
const next = Order.applyPatch(bytes, patch); // -> canonical bytes
```

The six operations are `add`, `remove`, `replace`, `move`, `copy`, and `test`. `add`/`replace`/`test` carry a `value`; `move`/`copy` carry a `from` pointer. On arrays, an integer index inserts (and `add` shifts the tail), and the token `-` refers to the position after the last element (append).

### Atomicity and errors

A patch is applied to a copy of the document and only returned if **every** operation succeeds. A failed `test`, a missing path for `remove`/`replace`/`move`, an out-of-range array index, or a structurally invalid operation throws `JsonPatchError` and leaves the input untouched — a partially-applied patch is never observable.

```typescript
try {
  Order.applyPatch(bytes, [{ op: "test", path: "/version", value: 99 }, /* ... */]);
} catch (e) {
  if (e instanceof JsonPatchError) { /* the document is unchanged */ }
}
```

`applyPatch` does **not** validate the result against the schema — a `move` can introduce a field the schema does not define. When the patch came from an untrusted source, evaluate the result: `Order.evaluate(next)`.

## RFC 7396 JSON Merge Patch

A merge patch is an ordinary JSON document overlaid on the target: each member replaces (or, for nested objects, recursively merges) the corresponding member, and a member set to `null` **deletes** that key. It is the natural shape for a partial update, at the cost of not being able to set a value *to* null or to patch individual array elements.

```typescript
const next = Order.applyMergePatch(bytes, {
  status: "shipped",          // replace
  shipping: { tracking: "Z" }, // merge into the existing shipping object
  draft: null,                 // delete the "draft" key
});
```

## Diffing two documents

`createPatch` and `createMergePatch` produce the change that turns one document into another — useful for storing or transmitting an edit, or for audit logs:

```typescript
const patch = Order.createPatch(before, after);        // RFC 6902 operations
const merge = Order.createMergePatch(before, after);    // RFC 7396 merge patch
// applying either to `before` reproduces `after`
```

`createPatch` emits a minimal `add`/`remove`/`replace` patch (it does not attempt to discover `move`/`copy` opportunities).

## See also

- [mutation](./mutation.md) — field-level `build` / `patch` / `produce`.
- [020-json-patch](./examples/020-json-patch/) — a runnable recipe for everything above.
