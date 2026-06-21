---
name: corvus-ctj-handler-implementation
description: >
  Implement OpenAPI server handlers using Corvus.Text.Json generated types.
  Covers the workspace-owned lifetime model, the Read/ReadMutable store pattern,
  builder pattern for responses, From<T>() for zero-copy cross-namespace values,
  TryX out-parameter pattern for lookups, EnumerateArray() iteration, and the
  two-hop cast requirement for Mutable→Source. USE FOR: implementing handler
  methods for generated IApi*Handler interfaces, reading/writing blob stores
  with correct document lifetime, building response bodies, avoiding memory
  leaks and use-after-free. DO NOT USE FOR: code generation itself
  (use corvus-codegen), mutable document manipulation details
  (use corvus-mutable-documents), parsing standalone documents
  (use corvus-parsed-documents-and-memory).
---

# CTJ Handler Implementation

## The Golden Rule

**Any data that reaches a response body must be owned by the workspace.**

The result factory (e.g., `ListTodosResult.Ok(body, workspace)`) calls `CreateBuilder(workspace, body)` which wraps the `Source`'s backing memory — it does NOT deep-copy. If the backing memory is freed before serialization, the response is corrupted.

## Document Lifetime

### Workspace-Owned (correct for handlers)

```csharp
var (builder, etag) = await store.ReadMutableAsync(sasToken, workspace, ct);
TodoList.Mutable list = builder.RootElement;
// builder is owned by workspace — lives until request ends
return ListTodosResult.Ok(body: (TodoList)list, workspace: workspace);
```

### Caller-Owned (only for inspect-and-discard)

```csharp
using var doc = await store.ReadAsync(sasToken, ct);
bool exists = doc is not null;
// Safe: nothing from doc reaches the response
return exists ? OkResult(...) : NotFoundResult(...);
```

### NEVER use ParseValue

`ParseValue()` is being marked `[Obsolete]`. It creates a self-owned copy with no deterministic disposal. Always use `ParsedJsonDocument<T>.Parse*()` or `JsonDocumentBuilder<T>.Parse(workspace, ...)`.

## Store Pattern: Read vs ReadMutable

Provide two overloads on your store type:

```csharp
public sealed class MyStore
{
    /// Read-only — caller owns and must dispose. For inspect-and-discard only.
    public async Task<ParsedJsonDocument<T>?> ReadAsync(
        JsonString sasToken, CancellationToken ct) { ... }

    /// Mutable — workspace owns lifetime. For any path where data reaches response.
    public async Task<(JsonDocumentBuilder<T.Mutable> Builder, ETag ETag)?> ReadMutableAsync(
        JsonString sasToken, JsonWorkspace workspace, CancellationToken ct) { ... }
}
```

**When to use which:**

| Scenario | Method |
|----------|--------|
| Data reaches response body (list, get, create, update) | `ReadMutableAsync` |
| Pure existence check, no data in response | `ReadAsync` with `using` |
| Mutation needed (add/remove/update items) | `ReadMutableAsync` |

## Building Response Bodies

### From literals/new data (Build pattern)

```csharp
return CreateTodoResult.Created(
    body: TodoItem.Build((ref TodoItem.Builder b) =>
    {
        b.Create(id: Guid.NewGuid(), title: body.Title, status: "pending"u8);
    }),
    workspace: workspace);
```

### From existing workspace-owned data (pass-through)

```csharp
var (builder, _) = await store.ReadMutableAsync(sasToken, workspace, ct);
// Cast needed: Mutable → T (one hop), then T → Source (implicit)
return ListTodosResult.Ok(body: (TodoList)builder.RootElement, workspace: workspace);
```

### The Two-Hop Cast

C# does not chain implicit conversions. `Mutable → T` is implicit, `T → Source` is implicit, but `Mutable → Source` requires an explicit cast to bridge the first hop:

```csharp
// ❌ Won't compile — no direct Mutable → Source conversion
return OkResult(body: mutableItem, workspace);

// ✅ Cast to immutable type; implicit conversion to Source handles the rest
return OkResult(body: (TodoItem)mutableItem, workspace);
```

**Note:** The CTJ002 analyzer may incorrectly flag this cast as unnecessary (see issue #775). Suppress or ignore — the cast is required.

### Emitting store-produced data the body holds past the handler (the deferred-body rule)

A response body's `Source` is **not** consumed when the handler returns — it is re-read later, during response validation/serialization (`ValidateBody`). So anything the body references must stay valid until *then*, not just until the handler exits:

- **A pooled document the body projects** must be handed to the workspace — single value `workspace.TakeOwnership(doc)`; a page/list `PooledDocumentList<T>.TransferOwnershipTo(workspace)` — so it outlives the handler. `using`-disposing it at handler return is a use-after-free (`ObjectDisposedException` at serialization). Inspect-and-discard checks that never reach the body are the only safe `using`.
- **A pooled/transient scalar written via a builder** (e.g. an opaque continuation token emitted `(JsonString.Source)tokenUtf8.Span`): a builder lambda **cannot capture a `Span`**, and a `stackalloc`/just-disposed buffer would dangle. Carry it as a **capturable `ReadOnlyMemory<byte>` owned by a disposable carrier** (the page) that the handler `using`-scopes: the synchronous `Ok(...)`/`Build(...)` copies the bytes into the response document while the carrier is alive; the carrier's `Dispose` returns the pooled buffer afterwards. Centralise the rent+encode in a page `Create(...)` factory so every backend call site stays a leak-free one-liner.

A primitive value written via `b.Create(prop: value)` is copied into the response document during the build — safe for a transient *only if* the build is synchronous and the value is alive throughout it, which the disposable-carrier pattern guarantees.

## Zero-Copy Cross-Namespace Values: From\<T\>()

Every generated type has a static `From<T>()` method that reinterprets backing memory as a different type — zero allocation:

```csharp
// Cross-namespace: DirectoryStorage.JsonString → Directory.JsonString
Directory.JsonString.From(user.Value.DisplayName)

// Same pattern for UUIDs, emails, etc.
Directory.JsonUuid.From(accepted.Ticket)
```

Use `From<T>()` whenever passing values between types from different generated namespaces. Within the same namespace, implicit conversion works directly.

## Array Enumeration

Generated array types do NOT implement `IEnumerable<T>`. You must call `EnumerateArray()`:

```csharp
// ❌ Won't compile
foreach (TodoItem item in list) { ... }

// ✅ Correct
foreach (TodoItem item in list.EnumerateArray()) { ... }

// Mutable arrays return mutable elements
foreach (TodoItem.Mutable item in list.EnumerateArray()) { ... }
```

## TryX Pattern for Lookups

Use `bool Try...(out T result)` rather than nullable returns:

```csharp
private static bool TryFindItem(TodoList list, JsonString todoId, out TodoItem foundItem)
{
    using var utf8Id = todoId.GetUtf8String();
    foreach (TodoItem item in list.EnumerateArray())
    {
        if (item.Id.ValueEquals(utf8Id.Span))
        {
            foundItem = item;
            return true;
        }
    }

    foundItem = default;
    return false;
}
```

## Conditional/Optional Values

For optional properties that may be undefined:

```csharp
// Check before accessing
if (!item.DueDate.IsUndefined())
{
    mutableItem.SetDueDate(update.DueDate);
}

// Ternary for Source construction
email: user.HasValue && !user.Value.Email.IsUndefined()
    ? Directory.JsonEmail.From(user.Value.Email)
    : default
```

`default` for a `Source` produces an undefined value (omitted from output).

## AddItem on Array Builders

Array builder `AddItem()` takes `T.Source`, not `T.Mutable`. Since C# won't chain two implicit conversions, cast mutable items:

```csharp
// ❌ Won't compile — Mutable has no direct → Source conversion
foreach (DirectoryUser.Mutable existing in directory.Users.EnumerateArray())
    ab.AddItem(existing);

// ✅ Cast to immutable (one hop), then implicit to Source (second hop)
foreach (DirectoryUser.Mutable existing in directory.Users.EnumerateArray())
    ab.AddItem((DirectoryUser)existing);
```

## Serialization

Use `Corvus.Text.Json.Utf8JsonWriter` (NOT `System.Text.Json.Utf8JsonWriter`):

```csharp
using var ms = new MemoryStream();
using (var writer = new Corvus.Text.Json.Utf8JsonWriter(ms))
{
    builder.WriteTo(writer);
}
ms.Position = 0;
await blob.UploadAsync(ms, options, ct);
```

## Common Pitfalls

| Pitfall | Consequence | Fix |
|---------|-------------|-----|
| `using ParsedJsonDocument` when data reaches response | Use-after-free / corrupted response | Use `ReadMutableAsync` into workspace |
| `ParseValue()` anywhere | Memory leak (no disposal) | Use `ParsedJsonDocument.Parse*()` or `JsonDocumentBuilder.Parse(workspace, ...)` |
| Missing cast `(T)mutable` when passing to `Source`-typed parameter | CS1503 compile error | Add explicit cast to immutable type |
| Removing cast because CTJ002 says unnecessary | CS1503 compile error (two-hop chain broken) | Keep the cast; suppress CTJ002 |
| `foreach (T item in array)` without `.EnumerateArray()` | CS1579 compile error | Use `.EnumerateArray()` |
| `From()` within same namespace | Unnecessary — implicit conversion works | Remove `From()`, use value directly |
| Returning `T?` from lookup helpers | Forces boxing/nullable overhead on struct types | Use `bool TryX(out T result)` pattern |
| Using `System.Text.Json.Utf8JsonWriter` | Wrong writer type; won't serialize CTJ types | Use `Corvus.Text.Json.Utf8JsonWriter` |

## Cross-References

- For workspace and builder mechanics, see `corvus-mutable-documents`
- For parsing and memory model, see `corvus-parsed-documents-and-memory`
- For code generation from OpenAPI specs, see `corvus-codegen`
- For analyzer diagnostics (CTJ001–CTJ006), see `corvus-analyzers`
