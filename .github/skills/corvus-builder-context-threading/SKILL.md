---
name: corvus-builder-context-threading
description: >
  Build a generated Corvus model (response body, array, nested object) from UTF-8 spans inside a hot
  loop with NO closure and NO managed-string round-trip, using the generated Build<TContext> /
  CreateBuilder<TContext> context-threading form (static lambdas + a ref-struct context that
  `allows ref struct`, so it can carry ReadOnlySpan<byte>). Covers the array Build<TContext> /
  AddItem<TContext> nesting, the implicit ReadOnlySpan<byte> -> JsonString.Source / enum Source
  operators, and the four ref-safety gotchas (internal Source ctor, Build-result ref escape on
  string->Source temporaries, Source<TContext> not converting to the non-generic Source, the optional
  span argument). USE FOR: projecting a list/page of bytes-backed values into a generated response
  without a per-item closure; threading spans into a builder; fixing CS8347/CS8350/CS8156/CS1729
  around generated builders. DO NOT USE FOR: building a model from native (string/int/JsonElement)
  values (use corvus-typed-model-construction), the broader bytes-to-bytes anti-pattern
  (use corvus-bytes-to-bytes), ref-struct callback signatures (use ref-struct-delegates).
---

# Building generated models from spans without a closure

A non-`static` builder lambda (`new T.Source((ref T.Builder b) => b.Create(value: localString))`)
captures its locals into a **closure** — a heap allocation per call — and **cannot capture a
`ReadOnlySpan<byte>`** at all (a `ref struct` can't be captured). The generated `Build<TContext>` /
`CreateBuilder<TContext>` form solves both: a **`static`** lambda (no capture, no closure) plus a
**ref-struct context** (`where TContext : allows ref struct`) that carries the spans. This is the form
the JMESPath/Jsonata/OpenApi generators emit; use it whenever you project bytes into a model in a loop.

## The pattern

```csharp
// 1. A ref-struct context carries the data (spans, the source list, anything) — it may hold ReadOnlySpan<byte>.
private readonly ref struct ItemState(ResolvedPrincipal principal, IReadOnlyList<CredentialUsageGrant> grants)
{
    public ResolvedPrincipal Principal { get; } = principal;
    public IReadOnlyList<CredentialUsageGrant> Grants { get; } = grants;
}

// 2. Static build methods match the generated `Build<TContext>(in TContext, ref Builder)` delegate.
private static void BuildGrantee(in ItemState item, ref Models.ResolvedGrantee.Builder grantee)
{
    grantee.Create(                                   // the Create<TContext> instance overload (TContext inferred from `in item`)
        in item,
        complete: true,
        identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildIdentity),  // nested array, same context
        kind: item.Principal.Kind.ToTokenUtf8(),       // ReadOnlySpan<byte> -> GranteeKind.Source (implicit)
        source: "directory"u8,                          // ReadOnlySpan<byte> -> enum Source (implicit)
        value: item.Principal.ValueMemory.Span,         // ReadOnlySpan<byte> -> JsonString.Source (implicit)
        label: item.Principal.HasLabel ? (Models.JsonString.Source)item.Principal.LabelMemory.Span : default);
}
```

The generated string/enum `Source` types implicitly convert from `ReadOnlySpan<byte>` (`JsonString.Source`,
`GranteeKind.Source`, an enum `SourceEntity.Source`), so `…Utf8()` spans and `"literal"u8` thread straight
into `Create`. Use `static` named methods (above) or `static` lambdas — never a capturing lambda.

## Arrays — Build<TContext> + AddItem<TContext>

```csharp
private static void BuildGrantees(in PageState s, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
{
    foreach (ResolvedPrincipal p in s.Found)
    {
        if (!s.Context.Admits(AccessVerb.Read, p.Identity)) { continue; }
        var item = new ItemState(p, s.Access.DescribeUsageScope(p.Identity));
        array.AddItem(Models.ResolvedGrantee.Build(in item, BuildGrantee));   // AddItem<TItemCtx>(in Source<TItemCtx>) — the item's TContext may differ from the array's
    }
}
```

`Array.Build<TContext>(in ctx, static …)` returns `Array.Source<TContext>`; the array builder's
`AddItem<TContext>(in T.Source<TContext>)` accepts an item whose context type differs from the array's.

## Reaching a non-generic-`Source` consumer (e.g. `SearchGranteesResult.Ok`)

`Source<TContext>` does NOT implicitly convert to the non-generic `Source`. When the consumer takes a
non-generic `Source` (the generated `…Result.Ok(GranteeList.Source body, ws)`), **materialize** with
`CreateBuilder<TContext>(…).RootElement` (the immutable converts to `Source` via its implicit operator):

```csharp
var state = new PageState(found, context, this.access);
Models.GranteeList grantees = Models.GranteeList.CreateBuilder(
    workspace,
    in state,
    Models.GranteeList.ResolvedGranteeArray.Build(in state, BuildGrantees)).RootElement;
return SearchGranteesResult.Ok(grantees, workspace);
```

## Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| **CS1729** `'T.Source' does not contain a constructor that takes 1 argument` (from another assembly, e.g. a benchmark) | the `new T.Source((ref Builder b) => …)` WriteAction ctor is **`internal`** | use the public factory `T.Build((ref T.Builder b) => …)` (same-assembly handler code can use `new(...)`) |
| **CS8347 / CS8350 / CS8156** on `array.AddItem(T.Build(strA, strB))` | the lazy `Build(in field, …)` holds a **ref** to its `in` args; a `string -> Source` arg is a stack temporary, so the lazy item can't be appended | for a genuine string-leaf sub-array (e.g. the `{dimension,value}` grants), use the WriteAction form `new T.Source((ref Builder b) => b.Create(strA, strB))` — a closure there is fine (the strings are the leaf), or thread the value via the context and use a span |
| ternary `label: hasLabel ? span : default` won't infer a common type | `ReadOnlySpan<byte>` vs `JsonString.Source default` | cast the span branch: `hasLabel ? (Models.JsonString.Source)span : default` |
| `Build(field: v)` result "may expose variables outside their scope" when **returned** from a helper | `Build` takes fields by `in`; its `Source` cannot escape the method | consume it in place (pass straight to the consumer / `AddItem`), never `return` it — see corvus-typed-model-construction |

## Cross-References

- `corvus-typed-model-construction` — the factory overview (`Build` / `CreateBuilder` / `CreateBuilder<TContext>`), the `JsonElement.Source` ref-safety trap, discriminated unions. Read it first for the basics; this skill is the spans-in-a-loop-no-closure advanced case.
- `corvus-bytes-to-bytes` — when/why to thread spans at all (the record<->document anti-pattern + the genuine-leaf proof).
- `ref-struct-delegates` — the named `ref struct` delegate types the build callbacks use.
- `corvus-mutable-documents` — `JsonWorkspace` / `JsonDocumentBuilder` and the materializing `CreateBuilder` path.
