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

**Scalar-copied vs `From`-wrapped (the precise rule).** `ResultType.Ok(body, workspace)` runs the body `Source` closure **synchronously** inside `CreateBuilder(workspace, body).RootElement`, so a `using`-scoped carrier is still alive when the closure runs. Within that closure two things behave differently at serialization time:
- A **scalar** written via `b.Create(token: (JsonString.Source)page.NextPageToken.Span)` is **copied** into the response document during `CreateBuilder` → the carrier's pooled buffer can be disposed right after `Ok` returns (read `.Span` *inside* the closure, never as a captured `Span` local). This is why the page can be `using`-scoped.
- A **`From(externalDoc)`-wrapped sub-document** (e.g. `CatalogVersionSummary.From(version)` over a pooled store doc) is **referenced**, not copied → it is re-read at post-handler serialization, so its backing must be handed over with `PooledDocumentList<T>.TransferOwnershipTo(workspace)`. A page can do **both**: `TransferOwnershipTo` its wrapped documents *and* `using`-dispose itself to return the pooled token buffer (the token was already copied).

**A carrier that owns a pooled buffer must be a `sealed class : IDisposable`, never a `readonly record struct`.** A record struct is copy-by-value, so two copies share one rented `byte[]` and `Dispose` double-returns it (pool corruption). A record struct may own a `PooledDocumentList` (a class — idempotent dispose) and get away with it, but the moment it also owns a rented buffer, convert it to a class (mirror `SourceCredentialPage`/`ObservedIdentityPage`; `WorkflowRunPage`/`CatalogPage` were converted from record structs for exactly this).

## Response projection: decision order (run this BEFORE writing a projection)

A response body is built from a stored document. Pick the mechanism in **this order** — most projections are over-built because the wrong rung was chosen, and that over-building is pure allocation (managed strings, per-item closures, rebuilt arrays):

1. **Is the response type *congruent* with the stored type?** (Same fields, same required set, nothing the response must hide.) If yes → **whole-document `From()`**, for the single-document response *and* the list:
   - Single: `return GetXResult.Ok(Models.XView.From(doc.RootElement), workspace)` + `workspace.TakeOwnership(doc)`.
   - List: per item `array.AddItem(Models.XView.From(item))` + `page.TransferOwnershipTo(workspace)`.
   - **The single-document and list responses MUST use the same mechanism.** A field-copy *list* whose single-document sibling is a whole-doc `From()` is a missed collapse — check the single-doc site first; if it wraps with `From()`, the list collapses too (per item + ownership transfer). (This was the `ToRuleSource`/`ToViewSource` bug: the list field-copied while create/get/update already wrapped.)
2. **Must the response hide stored fields?** (e.g. a summary that drops an internal `scopes`/`expiresAt`/`usageTags`.) *Only then* field-select — and carry each selected leaf **bytes-native**: `Models.JsonString.From(stored.RawAccessor)` / `Models.JsonDateTime.From(stored.RawAccessor)`, never `(string)stored.X` or the nullable `XxxValue`/`XxxOrNull` accessors (see below).
3. **Build the list/object closure-free.** Thread the context (`Build<TContext>`), don't capture in a lambda — see `corvus-builder-context-threading`. The list `Build<TContext>` is ref-scoped to its `in` argument, so build it **inline in the handler**, not in a returned helper.

### The `XxxOrNull` / `XxxValue` anti-pattern (the realising ternary)

Never feed a generated builder from the nullable convenience accessors:

```csharp
// ❌ XxxValue/XxxOrNull DISCARD Undefined-ness → the ternary's `default` writes a REAL value
//    (DateTimeOffset default = the EPOCH; string = null/empty), not an omitted field. Latent bug.
lastUpdatedAt: stored.UpdatedAtValue is { } v ? v : default,
description:   stored.DescriptionOrNull is { } d ? d : default,

// ✅ bare From() of the RAW accessor PROPAGATES Undefined → an absent field is OMITTED, no ternary
lastUpdatedAt: Models.JsonDateTime.From(stored.LastUpdatedAt),
description:   Models.JsonString.From(stored.Description),
```

The `if (x.IsNotUndefined()) { local = From(x); }` guard is the same smell — redundant, since `From()` already propagates Undefined.

### Up-front sweep — grep when you START projection work (find ALL instances at once, not serially)

```bash
# nullable-accessor-into-builder smell (then READ each hit to classify — see "not the smell" below)
grep -rnE "OrNull is \{ ?\}|Value is \{ ?\}" src/ --include=*.cs | grep -v /Generated/
# closure-based projection (prefer Build<TContext> context-threading)
grep -rn "new Models\..*\.Source((ref" src/ --include=*.cs | grep -v /Generated/
```

**Not the smell** (leave alone): nullable accessors used as query-filter predicates, expiry comparisons (`ExpiresAtValue is { } e && e <= now`), hand-written `Utf8JsonWriter` envelope projections to a *different* shape (unix-millis dates, table columns), and `string?`→`Source` CLI-settings bridges (a plain `string?`, no CTJ Undefined concept).

### Distrust a "can't use `From` here" comment

A comment that rules out a whole-doc `From()` ("the batch is disposed before serialization, so a `From()` wrap would dangle") may predate the ownership-transfer pattern. Re-derive against the current rule: `TransferOwnershipTo(workspace)` keeps the batch alive, so the list **can** wrap. Trusting that one stale comment hid the `ToViewSource` collapse for an entire campaign — see `corvus-builder-context-threading` and the memories on verifying before declaring impossible.

## Zero-Copy Cross-Namespace Values: From\<T\>()

Every generated type has a static `From<T>()` method that reinterprets backing memory as a different type — zero allocation:

```csharp
// Cross-namespace: DirectoryStorage.JsonString → Directory.JsonString
Directory.JsonString.From(user.Value.DisplayName)

// Same pattern for UUIDs, emails, etc.
Directory.JsonUuid.From(accepted.Ticket)
```

Use `From<T>()` whenever passing values between types from different generated namespaces. Within the same namespace, implicit conversion works directly.

`From<T>()` **propagates undefined**: `From(source)` of an undefined `source` is an undefined target (it reinterprets the same backing memory, which is still undefined). So do **not** guard it with an `IsNotUndefined()` ternary when the consumer already treats undefined as absent:

```csharp
// ❌ redundant — From() of an undefined PageToken is already an undefined JsonString
JsonString pageToken = parameters.PageToken.IsNotUndefined() ? JsonString.From(parameters.PageToken) : default;

// ✅ undefined flows straight through; the store's `if (pageToken.IsNotUndefined())` sees it either way
JsonString pageToken = JsonString.From(parameters.PageToken);
```

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
// Mutating a builder: guard the Set (you don't want to overwrite with undefined)
if (!item.DueDate.IsUndefined())
{
    mutableItem.SetDueDate(update.DueDate);
}

// Projecting INTO a builder: do NOT ternary-guard From() — it propagates Undefined,
// so an absent field is omitted with no ternary (see "The XxxOrNull/XxxValue anti-pattern").
email: Directory.JsonEmail.From(user.Email)
```

`default` for a `Source` produces an undefined value (omitted from output) — but you rarely need to write `default` explicitly, because a bare `From()` of an undefined source already yields it. Reach for a ternary only when the *true* branch is a non-CTJ type (a C# `string?`/`DateTimeOffset` with no source element to wrap — e.g. a CLI settings value); then cast the true branch to the `Source` type so `default` stays `default(Source)`.

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
| `XxxOrNull`/`XxxValue is { } v ? v : default` into a builder | Nullable accessor discards Undefined → `default` writes the epoch/empty, not an omitted field | Bare `Models.JsonX.From(stored.RawAccessor)` (propagates Undefined) |
| Field-copying a list whose single-doc sibling uses whole-doc `From()` | Over-allocation (managed strings + closures) for a congruent type | Collapse the list to per-item `From()` + `TransferOwnershipTo` |
| Capturing-lambda `new X.Source((ref b) => …)` per list item | A heap closure per item/array/list | `Build<TContext>` context-threading (inline in the handler) |

## Cross-References

- For workspace and builder mechanics, see `corvus-mutable-documents`
- For parsing and memory model, see `corvus-parsed-documents-and-memory`
- For code generation from OpenAPI specs, see `corvus-codegen`
- For analyzer diagnostics (CTJ001–CTJ006), see `corvus-analyzers`
