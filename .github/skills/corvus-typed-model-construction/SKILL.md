---
name: corvus-typed-model-construction
description: >
  Construct instances of generated Corvus strongly-typed models (DTOs, request/response
  bodies, discriminated-union variants) allocation-free, without composing or parsing JSON.
  Covers the Create() / Build() / CreateBuilder() / CreateBuilder<TContext>() factories,
  threading state through a context tuple with STATIC lambdas to avoid closures, the
  JsonElement.Source ref-safety trap and its fix, avoiding interim materializations, and
  building discriminated-union bodies. USE FOR: building a generated model from native
  values to pass to a generated client/handler, embedding a JsonElement/array value into a
  generated object, building union variants, fixing CS8168/CS8347/CS8350 around builders.
  DO NOT USE FOR: mutating an arbitrary JsonElement document (use corvus-mutable-documents),
  read-only parsing (use corvus-parsed-documents-and-memory), ref-struct callback signatures
  (use ref-struct-delegates).
---

# Typed Model Construction (allocation-free)

Generated Corvus types (`[JsonSchemaTypeGenerator]` / `openapi-client` / `openapi-server`
models) are built through value-typed factories that write **once** into a pooled arena. The
goal is to go from native values (`int`, `string`, a `JsonElement`) straight into the model
with **no interim string/JSON, no `ParseValue`, and no closure allocation**.

## Never compose-then-parse

```csharp
// ❌ interim string + parse + re-validate. Do not build request/response bodies this way.
var body = $$"""{"mode":"Rewind","targetCursor":{{n}}}""";
Models.ResumeRequest req = JsonElement.ParseValue(body);
```

Use the generated factories instead.

## The factories — pick by situation

| Factory | Returns | Use when |
|---|---|---|
| `T.Build(field: v, …)` | `T.Source` (lazy) | **The default.** Native values — including a `JsonElement`/array — passed straight in. Hand the `Source` to a consumer that materializes it once (a generated client/handler method). No workspace, no closure. |
| `T.Build(static (ref T.Builder b) => b.Create(…))` | `T.Source` (lazy) | No fields to set (e.g. a `const`-only union variant), or you need imperative logic. Lambda MUST be `static`. |
| `T.CreateBuilder(ws, field: v, …)` then `.RootElement` | `T` (immutable, in `ws`) | Only when you actually need a *materialized* value (e.g. to read it back, or store it). Not needed just to pass to a consumer. |
| `T.CreateBuilder<TContext>(ws, ctx, static (in TContext ctx, ref T.Builder b) => …)` then `.RootElement` | `T` | Materializing while threading runtime values into a builder loop/conditional. The form the JMESPath/Jsonata/OpenApi generators emit. |
| `T.CreateBuilder<TContext>(ws, in T.Source<TContext> body)` then `.RootElement` | `T` | Materializing a body you already assembled closure-free as a `Source<TContext>`. Usually you don't call this directly — the generated `…Result.Ok<TContext>(T.Source<TContext> body, ws)` does, in a **single** pass. See `corvus-builder-context-threading`. |

**Default to `T.Build(field: v, …)`** — it is lazy and the consumer materializes it directly
into its own buffer (one pass, no interim document). You almost never need `CreateBuilder` +
`.RootElement`; reach for it only when you genuinely need a materialized value in hand.

**Reaching a server result factory closure-free:** build the body as a context-threaded `T.Source<TContext>`
(`T.Build<TContext>(in ctx, …)`, the context a `RefTuple`) and hand it to the generic `…Result.Ok<TContext>(body, ws)`
overload — one closure-free materialization. Do **not** `CreateBuilder<TContext>(…).RootElement` then pass the immutable
to the non-generic `Ok` (that re-materializes). Full detail: `corvus-builder-context-threading`.

```csharp
// ✅ lazy, no workspace, no materialization — pass the builder straight to the client
await client.ResumeRunAsync(runId, RewindResume.Build(targetCursor), ct);                 // int
await client.CancelRunAsync(runId, CancelRequest.Build(reason), ct);                       // string
using var doc = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(path));
await client.ResumeRunAsync(runId, SkipResume.Build(skipOutputs: doc.RootElement), ct);    // JsonElement, passed directly
```

**Don't hand-roll the `Source` constructor when `Build` covers it.** A plain field set is
`T.Build(field: v, …)`. Reach for the raw `new T.Source((ref T.Builder b) => b.Create(…))`
constructor only for genuine imperative logic — not to set a couple of fields. (Older code in the
tree uses the constructor form for simple bodies; prefer `Build`.)

```csharp
// ❌ hand-rolled constructor for a plain field set
var body = new Models.MemberWrite.Source((ref Models.MemberWrite.Builder b) => b.Create(value: v, dimension: d));
// ✅ the Build factory
var body = Models.MemberWrite.Build(value: v, dimension: d);
```

**`Build` takes its fields by `in`, so consume its result in place — don't return it from a
helper.** Because `Build(in field, …)` holds a ref to each argument, its `Source` cannot escape
the method that built it: pass it **directly** to the consumer in the same expression (the common
case). Wrapping `Build` in a method that *returns* the `Source` fails with CS8347 / CS8156 ("may
expose variables … outside their declaration scope" / "cannot be … returned by reference"). If you
want a named local, build and consume it in the same scope.

```csharp
// ✅ consumed in place
await client.AddAdministratorAsync(baseId, Models.MemberWrite.Build(value: v, dimension: d), ct);

// ❌ returning Build's result escapes the in-ref (CS8347/CS8156)
static Models.MemberWrite.Source Member(string d, string v) => Models.MemberWrite.Build(value: v, dimension: d);
```

## The ref-safety trap (the one real gotcha)

`JsonElement.Source` is a ref struct created from `in JsonElement` — it holds a **ref** to the
element. Passing a `JsonElement` *directly as an argument* to a `Build(in …)`/`CreateBuilder(in …)`
factory is fine (the factory call's scope keeps the source document alive). What fails is
**capturing** a `JsonElement` in a (non-static) builder *lambda* — the captured ref escapes into
the closure and the compiler rejects it:

```csharp
// ❌ CS8168 / CS8347 / CS8350 — outputs is captured; its ref escapes the closure
JsonElement outputs = doc.RootElement;
var src = new SkipResume.Source((ref SkipResume.Builder b) => b.Create(skipOutputs: outputs));

// ✅ pass it to the native-field factory instead — no lambda, no capture
var src = SkipResume.Build(skipOutputs: doc.RootElement);
```

If you genuinely need imperative building *and* a threaded `JsonElement`, use the `TContext`
form so the value flows through `scoped in` rather than a closure:

```csharp
var value = T.CreateBuilder(
    workspace,
    (src: someElement, ws: workspace),
    static (in (JsonElement src, JsonWorkspace ws) ctx, ref T.Builder b) => { /* use ctx.src */ }).RootElement;
```

Always make builder lambdas `static` and thread state via the context tuple — a non-static
lambda allocates a closure and reintroduces the capture/escape problem.

## Discriminated unions (oneOf)

Build the **variant**, not the union; the variant `Source` implicitly converts to the union's
(non-generic) `Source`, so you pass the builder straight to the consumer — no materialization:

```csharp
// RewindResume.Source / StatePatchResume.Source  -->  ResumeRequest.Source (implicit) --> client
await client.ResumeRunAsync(runId, RewindResume.Build(targetCursor), ct);
await client.ResumeRunAsync(runId, StatePatchResume.Build(patchArray), ct);
```

`const`-discriminated variants set their own discriminator inside `Build`/`Create`, so you never
set `mode` (etc.) yourself.

Caveat (rare): a variant's **`Source<TContext>` does NOT implicitly convert** to the union's
non-generic `Source`. So if you must use the `TContext` threading form for a union body,
materialize that variant via `CreateBuilder<TContext>(ws, …).RootElement` and pass the immutable
(which converts via the `Source(Variant instance)` operator). The native-field `Build(field: v)`
factory avoids this entirely, so prefer it.

## Lifetime

The immutable returned by `.RootElement` is a view over the `workspace` (and `.Source` lazies
reference whatever they were built from). Keep the `JsonWorkspace` / source `ParsedJsonDocument`
alive until the consumer has finished writing the value (e.g. across the `await` on a client
call). All of `JsonWorkspace`, `JsonDocumentBuilder<T>`, `ParsedJsonDocument<T>` are `using`-disposable.

## Conversions cheat-sheet

- `string` → `JsonString.Source`, `int` → `JsonInt32.Source`, `int`/`long` → `JsonInteger`/`Schema.Source`, `DateTimeOffset`/ISO `string` → `JsonDateTime.Source` — all implicit.
- `JsonElement` → `JsonElement.Source` (implicit, **by-ref** — the trap above).
- A generated value `T` → its containing union's `Source` (implicit). Chained two-hop user
  conversions are NOT allowed at an argument — introduce an intermediate local for the first hop.

## Cross-References

- `corvus-builder-context-threading` — building from UTF-8 **spans** in a loop with no closure (the `Build<TContext>` / `CreateBuilder<TContext>` form and its ref-safety gotchas); reach for it when the values are spans, not native `string`/`int`.
- `corvus-bytes-to-bytes` — when to thread spans at all (the record<->document string-seam anti-pattern + the genuine-leaf proof).
- `corvus-mutable-documents` — `JsonWorkspace` / `JsonDocumentBuilder` and mutating arbitrary `JsonElement` documents.
- `ref-struct-delegates` — why builder callbacks use named `Build` delegates (ref struct params) not `Func<>`/`Action<>`.
- `corvus-buffer-and-pooling` — the pooling that backs the workspace arena.
- `corvus-codegen` — how these factories are generated.
