---
name: corvus-builder-context-threading
description: >
  Build a generated Corvus model (response body, array, nested object) from UTF-8 spans inside a hot
  loop with NO closure and NO managed-string round-trip, using the generated Build<TContext> /
  CreateBuilder<TContext> context-threading form (static lambdas + a ref-struct context that
  `allows ref struct`, so it can carry ReadOnlySpan<byte>). The context is a RefTuple<…> (the
  span-capable ValueTuple companion, .NET 9+) instead of a bespoke context struct. Hand the
  context-threaded body to the result factory's generic Ok<TContext>(Source<TContext>, ws) overload
  for a single closure-free materialization (NOT CreateBuilder<TContext>().RootElement + non-generic
  Ok, which re-materializes). Covers the array Build<TContext> / AddItem<TContext> nesting, the implicit
  ReadOnlySpan<byte> -> JsonString.Source / enum Source operators, RefTuple, and the ref-safety gotchas.
  USE FOR: projecting a list/page of bytes-backed values into a generated response without a per-item
  closure; threading spans into a builder; reaching the Ok<TContext> boundary; fixing
  CS8347/CS8350/CS8156/CS1729 around generated builders. DO NOT USE FOR: building a model from native
  (string/int/JsonElement) values (use corvus-typed-model-construction), the broader bytes-to-bytes
  anti-pattern (use corvus-bytes-to-bytes), ref-struct callback signatures (use ref-struct-delegates).
---

# Building generated models from spans without a closure

A non-`static` builder lambda (`new T.Source((ref T.Builder b) => b.Create(value: localString))`)
captures its locals into a **closure** — a heap allocation per call — and **cannot capture a
`ReadOnlySpan<byte>`** at all (a `ref struct` can't be captured). The generated `Build<TContext>` /
`CreateBuilder<TContext>` form solves both: a **`static`** lambda (no capture, no closure) plus a
**ref-struct context** (`where TContext : allows ref struct`) that carries the spans. This is the form
the JMESPath/Jsonata/OpenApi generators emit; use it whenever you project bytes into a model in a loop.

## The context — `RefTuple`, the span-capable carrier

Thread the per-item data through a **`RefTuple<…>`** — the span-capable companion to `ValueTuple` (`Corvus.Text.Json`,
.NET 9+). Because its element type parameters are `allows ref struct`, a `RefTuple` element **can be a
`ReadOnlySpan<byte>`** — which `ValueTuple` cannot hold at all. So you no longer hand-roll a bespoke context `ref struct`
per call site; `Deconstruct` recovers named locals (`var (page, access) = state;`). Use it for both the array-level and
the per-item context. (On targets older than .NET 9, `RefTuple` does not exist — use a bespoke `ref struct` there.)

```csharp
// Static build methods match the generated `Build<TContext>(in TContext, ref Builder)` delegate; the context is a RefTuple.
private static void BuildGrantee(in RefTuple<ResolvedPrincipal, IReadOnlyList<CredentialUsageGrant>> item, ref Models.ResolvedGrantee.Builder grantee)
{
    ResolvedPrincipal principal = item.Item1;
    grantee.Create(                                   // the Create<TContext> instance overload (TContext inferred from `in item`)
        in item,
        complete: true,
        identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildIdentity),  // nested array, same context
        kind: principal.Kind.ToTokenUtf8(),            // ReadOnlySpan<byte> -> GranteeKind.Source (implicit)
        source: "directory"u8,                          // ReadOnlySpan<byte> -> enum Source (implicit)
        value: principal.ValueMemory.Span,              // ReadOnlySpan<byte> -> JsonString.Source (implicit)
        label: principal.HasLabel ? (Models.JsonString.Source)principal.LabelMemory.Span : default);
}
```

### RefTuple carrying a span (`TContext` with a ref-type element)

The case `ValueTuple` can't express — put the `ReadOnlySpan<byte>`s **directly** in the context and `Deconstruct` them
into named span locals inside the `static` builder (no bespoke struct, no closure, no managed string):

```csharp
ReadOnlySpan<byte> value = principal.ValueMemory.Span;
ReadOnlySpan<byte> label = principal.LabelMemory.Span;
var item = new RefTuple<ReadOnlySpan<byte>, ReadOnlySpan<byte>, GranteeKind>(value, label, principal.Kind);

array.AddItem(Models.ResolvedGrantee.Build(in item,
    static (in RefTuple<ReadOnlySpan<byte>, ReadOnlySpan<byte>, GranteeKind> i, ref Models.ResolvedGrantee.Builder g) =>
    {
        var (v, l, kind) = i;                          // named span locals via Deconstruct
        g.Create(complete: true, kind: kind.ToTokenUtf8(), source: "directory"u8, value: v, label: (Models.JsonString.Source)l);
    }));
```

The generated string/enum `Source` types implicitly convert from `ReadOnlySpan<byte>` (`JsonString.Source`,
`GranteeKind.Source`, an enum `SourceEntity.Source`), so `…Utf8()` spans and `"literal"u8` thread straight
into `Create`. Use `static` named methods (above) or `static` lambdas — never a capturing lambda.

## Arrays — Build<TContext> + AddItem<TContext>

The array-level context is its own `RefTuple` (the page/source list + whatever the loop needs); each item gets its own
`RefTuple`. The two contexts differ, and that's fine — `AddItem<TItemCtx>` accepts an item whose context type differs
from the array's.

```csharp
private static void BuildGrantees(in RefTuple<IReadOnlyList<ResolvedPrincipal>, AccessContext, ControlPlaneAccess> s, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
{
    (IReadOnlyList<ResolvedPrincipal> found, AccessContext context, ControlPlaneAccess access) = s;   // Deconstruct
    foreach (ResolvedPrincipal p in found)
    {
        if (!context.Admits(AccessVerb.Read, p.Identity)) { continue; }
        var item = new RefTuple<ResolvedPrincipal, IReadOnlyList<CredentialUsageGrant>>(p, access.DescribeUsageScope(p.Identity));
        array.AddItem(Models.ResolvedGrantee.Build(in item, BuildGrantee));   // AddItem<TItemCtx>(in Source<TItemCtx>) — the item's TContext may differ from the array's
    }
}
```

`Array.Build<TContext>(in ctx, static …)` returns `Array.Source<TContext>`; the array builder's
`AddItem<TContext>(in T.Source<TContext>)` accepts an item whose context type differs from the array's.

## Reaching the response boundary — `Ok<TContext>` (closure-free AND single materialization)

The generated server result factory has a **generic overload** `…Result.Ok<TContext>(Body.Source<TContext> body, ws)`
(and `Created<TContext>`, etc.) that takes the context-threaded body **directly** and materializes it in **one** pass —
it routes through the model's `CreateBuilder<TContext>(in Source<TContext>)`. So build the whole body lazily as a
`Body.Source<TContext>` (via `Body.Build<TContext>(in ctx, …fields…)`) and hand it straight over:

```csharp
var state = new RefTuple<IReadOnlyList<ResolvedPrincipal>, AccessContext, ControlPlaneAccess>(found, context, this.access);
var body = Models.GranteeList.Build(
    in state,
    grantees: Models.GranteeList.ResolvedGranteeArray.Build(in state, BuildGrantees));
return SearchGranteesResult.Ok(body, workspace);   // Ok<TContext> — TContext inferred from `body`; one CreateBuilder<TContext> pass
```

**Do NOT** pre-materialize with `CreateBuilder<TContext>(…).RootElement` and pass the immutable to the *non-generic*
`Ok(immutable, ws)`: that re-materializes the body a **second** time (the immutable is rebuilt into the response
workspace). It is the slower, wrong shape. Hand the lazy `Source<TContext>` to `Ok<TContext>` instead — closure-free
*and* a single pass (the measured ~2.0 KB / fastest floor; see `GranteeProjectionBenchmarks`).

Why it works: `Source<TContext>` does NOT implicitly convert to the non-generic `Source`, which is why the result type
exposes a `Ok<TContext>` overload at all — it is the generic counterpart of `Ok(Source, ws)`. Any-schema response bodies
resolve to the universal `JsonElement`, which has the same `CreateBuilder<TContext>(in Source<TContext>)` overload, so
`Ok<TContext>` works for them too.

## Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| **CS1729** `'T.Source' does not contain a constructor that takes 1 argument` (from another assembly, e.g. a benchmark) | the `new T.Source((ref Builder b) => …)` WriteAction ctor is **`internal`** | use the public factory `T.Build((ref T.Builder b) => …)` (same-assembly handler code can use `new(...)`) |
| **CS8347 / CS8350 / CS8156** on `array.AddItem(T.Build(strA, strB))` | the lazy `Build(in field, …)` holds a **ref** to its `in` args; a `string -> Source` arg is a stack temporary, so the lazy item can't be appended | for a genuine string-leaf sub-array (e.g. the `{dimension,value}` grants), use the WriteAction form `new T.Source((ref Builder b) => b.Create(strA, strB))` — a closure there is fine (the strings are the leaf), or thread the value via the context and use a span |
| ternary `label: hasLabel ? span : default` won't infer a common type | `ReadOnlySpan<byte>` vs `JsonString.Source default` | cast the span branch: `hasLabel ? (Models.JsonString.Source)span : default` |
| `Build(field: v)` result "may expose variables outside their scope" when **returned** from a helper | `Build` takes fields by `in`; its `Source` cannot escape the method | consume it in place (pass straight to the consumer / `AddItem`), never `return` it — see corvus-typed-model-construction |
| a nested object's array field built with one context but the parent `Create<TContext>` wants another | `Create<TContext>(in ctx, …, nested: Nested.Source<TContext>, …)` takes the nested value at the **parent's** `TContext` — same type | thread the **same** context into the nested `Build` (`Build(in item, …)`); when two call sites carry contexts that differ only in one element (e.g. `RefTuple<ResolvedPrincipal,…>` vs `RefTuple<ObservedIdentity,…>`) a **single generic** static method over that element works — `BuildIdentity<TIdentity>(in RefTuple<TIdentity, …> item, ref Builder) where TIdentity : allows ref struct` — method-group inference fixes `TIdentity` from the `in item` argument at each call site (verified; no per-context duplication needed) |
| `Ok<TContext>` not emitted for a response whose body is scalar (`type: string`) | only object/array bodies carry a `Source<TContext>`; the generator gates the generic overload on that | expected — a scalar body uses the non-generic `Ok(Source, ws)`; there is nothing to thread |

## Cross-References

- `corvus-typed-model-construction` — the factory overview (`Build` / `CreateBuilder` / `CreateBuilder<TContext>`), the `JsonElement.Source` ref-safety trap, discriminated unions. Read it first for the basics; this skill is the spans-in-a-loop-no-closure advanced case.
- `corvus-bytes-to-bytes` — when/why to thread spans at all (the record<->document anti-pattern + the genuine-leaf proof).
- `ref-struct-delegates` — the named `ref struct` delegate types the build callbacks use.
- `corvus-mutable-documents` — `JsonWorkspace` / `JsonDocumentBuilder` and the materializing `CreateBuilder` path.
