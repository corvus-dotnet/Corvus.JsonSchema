# TypeScript Patterns - JSON Patch and Merge Patch

This recipe shows applying and building [RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902) and [RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396) documents against a generated type's bytes.

## The Pattern

The `patch` and `produce` methods edit a document by naming the fields you want to change. **JSON Patch** is the standard, interchange-friendly alternative. A patch is a JSON array of operations (`add` / `remove` / `replace` / `move` / `copy` / `test`, each with a JSON Pointer path) that you can receive over the wire, store, or compute as a diff. **JSON Merge Patch** is the simpler form for partial updates: a JSON document that overlays the target, where a member set to `null` deletes that key.

For every buildable type the generator emits four companion methods over the document bytes (or a parsed value):

- `Contact.applyPatch(doc, patch)`: apply an RFC 6902 patch, returning canonical bytes. The patch is applied **atomically**. A failed `test` (or any invalid operation) throws `JsonPatchError` and leaves the input untouched.
- `Contact.createPatch(source, target)`: compute a minimal RFC 6902 patch that turns `source` into `target`.
- `Contact.applyMergePatch(doc, mergePatch)`: apply an RFC 7396 merge patch, returning canonical bytes.
- `Contact.createMergePatch(source, target)`: compute the RFC 7396 merge patch between two documents.

The generic `applyPatch` / `createPatch` / `applyMergePatch` / `createMergePatch` functions (which work on parsed JSON values) and the `JsonPatch` / `JsonPatchOp` types and `JsonPatchError` are re-exported from every module.

## The Schema

File: [`contact.json`](./contact.json)

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

## Generated Code Usage

[Example code](./demo.ts)

### Apply an RFC 6902 patch

```typescript
import { Contact, type JsonPatch } from "./generated.js";

const patch: JsonPatch = [
  { op: "test", path: "/version", value: 1 },             // guard: only apply if version is 1
  { op: "replace", path: "/version", value: 2 },
  { op: "add", path: "/phones/-", value: "+1-555-0199" }, // "-" appends to the array
  { op: "copy", from: "/name", path: "/displayName" },
  { op: "remove", path: "/address/zip" },
];
const patched = Contact.applyPatch(original, patch); // -> canonical bytes
```

A failed `test` aborts the whole patch (it throws `JsonPatchError` and the input is unchanged).

### Apply an RFC 7396 merge patch

```typescript
const merged = Contact.applyMergePatch(original, {
  email: "ada@new.example.com",  // replace
  address: { zip: null },         // delete address.zip, keep address.city
  phones: null,                   // delete phones entirely
});
```

### Diff two documents

```typescript
const patch = Contact.createPatch(original, target);            // RFC 6902 ops
const merge = Contact.createMergePatch(original, target);       // RFC 7396 merge patch
// applying the diff to `original` reproduces `target`
```

## Running the Example

From `docs/typescript/examples/` (run `npm install` once):

```bash
npm run build
node dist/020-json-patch/demo.js
```

## Related Patterns

- [016-mutation](../016-mutation/): `build` / `patch` / `produce` field-level mutation in depth
- [001-data-object](../001-data-object/): the generated companion surface

## Frequently Asked Questions

### How is `applyPatch` different from `patch`?

`Contact.patch(bytes, changes)` is a typed, field-level update. You pass a `Partial<Contact>` of the fields to change. `Contact.applyPatch(bytes, ops)` applies a standard **RFC 6902** patch document (an array of `{ op, path, value }` operations with JSON Pointer paths) which is the right choice when the patch arrives over the wire, is stored, or is computed as a diff.

### Does `applyPatch` validate the result against the schema?

No. It applies the operations and returns the resulting bytes; the result may not match the schema (for example a `move` to a path the schema does not define). Validate the result with `Contact.evaluate(...)` if the patch came from an untrusted source.

### Is the patch applied atomically?

Yes. Operations are applied to a copy, and any failure (a failed `test`, a missing path for `remove`/`replace`, an invalid operation) throws `JsonPatchError` without mutating the input, so a partially-applied patch is never observable.
