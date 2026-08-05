---
name: corvus-bytes-to-bytes
description: >
  Eliminate hand-rolled POCO record<->document string seams — types/paths that materialize a
  managed string (or List<string>/Dictionary) between a bytes SOURCE (a parsed UTF-8 body, a DB
  column, a directory/HTTP response) and a bytes SINK (a serialized JSON document, a generated
  model, a SecurityTagSet, a DB write). The fix recipe is bytes-native default + opt-in string
  extensibility, plus a decision procedure for telling a genuine string leaf from work you are
  avoiding. USE FOR: deciding whether a string on a hot/warm path is justified; converting a
  bytes->string->bytes u-turn to bytes-to-bytes; reviewing your own diff for hidden string
  materializations before committing; building an identity/tag set from a UTF-8 source.
  DO NOT USE FOR: the mechanics of building a generated model from spans without a closure
  (use corvus-builder-context-threading), raw pooling/stackalloc patterns
  (use corvus-buffer-and-pooling), constructing models from native values
  (use corvus-typed-model-construction).
---

# Bytes-to-bytes: killing record<->document string seams

A **record<->document string seam** is the most expensive allocation anti-pattern in this codebase
and the one most often re-introduced. It is a value that arrives as **bytes**, is materialized into
a managed **string** (or `List<string>` / `Dictionary<string,…>` / a per-row POCO), and is then
written back out as **bytes** — a u-turn through the managed heap. Both ends are bytes; the string is
pure overhead, plus a closure if a non-`static` builder lambda is involved.

The cost is real and measured: a per-row row-security scan was **25.78 KB -> 1.56 KB (0.06x)** once the
filter walked the persisted UTF-8 instead of `SecurityTagSet.ToList()` per row; a directory grantee
projection was **5.88 KB -> 2.01 KB (0.34x)** once value/label flowed as spans instead of `GetString`.

## The genuine-leaf proof (run this BEFORE you write a string)

The words **"genuine leaf"**, **"marginal"**, **"admin-rare"**, **"low-frequency"**, **"pragmatic"**,
**"good enough"**, **"consistency win"**, **"too fragile"**, **"edge case"**, **"one X's worth"** are red
flags: they are usually a justification reached for *first*, to license skipping the bytes mechanism. Before any of them excuses a managed string on a
warm/hot path, write a **two-ended trace** and check both ends:

| | Source end | Destination end |
|---|---|---|
| **String IS the leaf only if** | a string-typed external API: `ClaimsPrincipal` claims, the Novell LDAP client (`LdapAttribute.StringValue`), a `BsonValue.AsString` (the driver pre-materialized it) | a string-typed sink: an `HttpRequestMessage` URI, an LDAP filter, an HTTP `Authorization` header, a `string`-keyed store (`store.GetAsync(string)`), a human-facing audit/error message |

**If EITHER end is bytes/spans and a documented mechanism exists, it is NOT a leaf — it is work you
are avoiding.** A "constructed" value (e.g. `first + " " + last`, a `prefix + dimension` key) is never
a leaf: assemble it into a pooled/stack UTF-8 buffer, not a string. The mechanism always exists:

| Need | Mechanism | Skill |
|---|---|---|
| Build a tag set from UTF-8 | `SecurityTagSet.Build` + `IdentityBuilder.Add(span)` | this skill |
| Build a key `prefix + dimension` | `stackalloc`/`ArrayPool` + `Encoding.UTF8.GetBytes(prefix, key)` | corvus-buffer-and-pooling |
| Write UTF-8 into a generated model, no closure | `T.Build<TContext>` / `CreateBuilder<TContext>` | corvus-builder-context-threading |
| Compare UTF-8 vs a fixed string | pre-encode the string to `u8`/`byte[]` once, `SequenceEqual` | this skill |
| Re-transcode a holder's bytes | `Utf8JsonWriter` -> `TagSet.CopyFromJsonArray` | corvus-mutable-documents |

### When the excuse is "too fragile" / "edge case" (a self-imposed invariant)

Sometimes the string (or the `List` + per-item concat that builds it) isn't defended as a *leaf* but as
too risky to remove — "the sort has a `'-'` vs `'.'` edge case needing a careful comparer", "I'd have to
reproduce the exact output bytes", "it's only one row's worth". That difficulty is almost always
**self-imposed**: it comes from preserving the existing code's *incidental* output shape (an entry/element
order, a container layout, an insertion order), not from the task itself. Before you skip it: (1) name the
exact invariant making it fragile; (2) read the **consumer** — the reader, the sink, the comparer — and
check whether it actually requires that invariant. An order-independent reader (finds entries by
name/key, not position), a sink that re-normalises/re-canonicalises, or a content-addressed-by-hash
artifact does **not**. When no consumer requires it, the invariant is incidental — drop it and the clean
low-alloc form falls out. Example: `WorkflowPackage.PackPooled` sorts sources by **key** in a pooled
scratch array and emits a fixed bucket order (workflow, sources, metadata), writing each entry name as
UTF-8 directly (`"sources/"u8` + key + `".json"u8`, length back-patched) — instead of reproducing the old
full-name sort, which removed the `List` + per-source name string outright (`PackCanonicalPackage`
0.73 -> 0.49 KB, scales per source). Treating incidental output shape as a contract is the
anchoring-on-existing-code failure (deriving the replacement from what the old code happened to do)
applied to behaviour instead of style.

### Opaque tokens are a carrier seam too ("store-minted, so it stays a string" is a rationalization)

An opaque pagination/continuation token round-trips between two UTF-8 ends — emitted into a JSON
response, carried back in the next JSON request (a CTJ `JsonString`) — so "it is store-minted, not domain
data, so it stays a string" is a genuine-leaf rationalization: both ends are bytes. Encode it bytes-native
with `System.Buffers.Text.Base64Url` (`GetEncodedLength` + `EncodeToUtf8` straight into the destination;
`GetMaxDecodedLength` + `DecodeFromUtf8` from the request's `pageToken.GetUtf8String().Span`) — no
`EncodeToString`/`DecodeFromChars` char detour, and no per-part `ToString()`/concat/`GetBytes` (assemble
the key into a `stackalloc`/`ArrayPool` UTF-8 buffer with a separator byte). The token must survive into
the response, but **owned ≠ GC**: pool it in a disposable carrier rather than minting a `string` (the only
*necessarily*-GC form). The cross-root request→store `JsonString` is bridged with `JsonString.From(...)`
(free rewrap). The lifetime that governs *emitting* it is the deferred-body rule — see
`corvus-ctj-handler-implementation`.

## The fix recipe — bytes-native default + opt-in string

Mirror `IdentityBuilder.Add(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)` (the fast path) vs
`Add(ReadOnlySpan<byte> key, string value)` (the opt-in for a value a deployment genuinely computed
through a string API). The bytes path is *the* path; the string path is the explicit, documented
exception — never the default.

```csharp
// The bytes-native seam every adapter/handler builds an identity through. The value span is the
// unescaped UTF-8 the source already holds (reader.GetUtf8String().Span); no managed string per tag.
SecurityTagSet identity = SecurityTagSet.Build(
    in state,
    static (ref IdentityBuilder builder, in TState s) =>
    {
        builder.Add("sys:tenant"u8, s.TenantSpan);   // span path
        builder.Add("sys:sub"u8, s.SubSpan);
    });
```

A deferred holder (`SecurityTagSet`, `TagSet`) is the bytes form. Read it as `((JsonElement)x).GetUtf8String()`
(a `ref struct`; cannot cross an `await`) or `.TakeOwnership(out byte[]? rented)` for owned
`ReadOnlyMemory<byte>` that can. Transcode to a `string` ONLY at the genuine leaf (e.g.
`Encoding.UTF8.GetString(value.Span)` for a `string`-keyed store key).

### Dual constructor for an extension-point contract

When a type is a deployment-authored extension point (e.g. `ResolvedPrincipal`, returned by an
`IDirectoryIdentityMapper`), give it BOTH constructors — span for the built-in fast path, string for
mapper ergonomics — and decode on demand for string consumers:

```csharp
public readonly struct ResolvedPrincipal
{
    private readonly ReadOnlyMemory<byte> value;   // owned UTF-8

    // Span ctor — the built-in adapters' bytes-to-bytes fast path (copies the transient span to owned).
    public ResolvedPrincipal(GranteeKind kind, ReadOnlySpan<byte> value, ReadOnlySpan<byte> label, bool hasLabel, SecurityTagSet identity) { /* value.ToArray() */ }
    // String ctor — the ergonomic path a deployment mapper (or LDAP, a genuine string leaf) uses.
    public ResolvedPrincipal(GranteeKind kind, string value, string? label, SecurityTagSet identity) { /* Encoding.UTF8.GetBytes(value) */ }

    public ReadOnlyMemory<byte> ValueMemory => this.value;                 // server: bytes-to-bytes into the response
    public string Value => Encoding.UTF8.GetString(this.value.Span);       // CLI/tests: decode on demand
}
```

### Constructed value -> pooled buffer (never a string)

```csharp
// A "first last" display name has no single source span — assemble it into a REUSED pooled buffer
// (rented once outside the row loop, grown on demand), not Encoding.UTF8.GetString(...) + string concat.
int needed = first.Length + 1 + last.Length;
if (labelBuffer is null || labelBuffer.Length < needed)
{
    if (labelBuffer is not null) { ArrayPool<byte>.Shared.Return(labelBuffer); }
    labelBuffer = ArrayPool<byte>.Shared.Rent(needed);
}
first.CopyTo(labelBuffer);
labelBuffer[first.Length] = (byte)' ';
last.CopyTo(labelBuffer.AsSpan(first.Length + 1));
ReadOnlySpan<byte> labelSpan = labelBuffer.AsSpan(0, needed);   // the consumer copies it before the next row reuses the buffer
```

## Walking a holder's UTF-8 instead of materializing it

When a path must *evaluate* over a tag set (not just copy it), parse the persisted UTF-8 once into a
pooled scratch + slice table (`SecurityTagSpanSort.Parse`), compare on spans, and pre-encode the fixed
side once. The string evaluator becomes a thin adapter that delegates via `FromTags`, so the existing
tests lock the bytes evaluator's semantics. See `SecurityRule.EvaluateAll(in SecurityTagSet, in Utf8ClaimSet)`
and `SecurityRuleEvaluation.cs`. Ordinal UTF-8 `SequenceEqual` IS ordinal string equality for the same
code points.

## Pre-commit self-audit (mandatory — see copilot-instructions.md pre-commit gate)

Before committing, scan your own diff and **report** each:

- [ ] New managed `string` / `List<string>` / `Dictionary` on a path where bytes are available? -> apply the proof; fix or flag.
- [ ] New non-`static` builder lambda (a closure) where a `static` + `TContext` form exists? -> corvus-builder-context-threading.
- [ ] Reflection-based dispatch where a virtual/span seam exists? -> remove it.
- [ ] Any work deferred / skipped / relocated? -> surface it under `Decisions & deferrals` + a task, never a buried comment.
- [ ] Did a fix MOVE a cost (a transcode/alloc) elsewhere rather than remove it? -> state the before/after `file:line`.

Prove every warm path with a BenchmarkDotNet `[MemoryDiagnoser]` benchmark (baseline old vs new) — see
`SecurityFilterScanBenchmarks` / `GranteeProjectionBenchmarks` for the shape. "Admin-rare" is not a
licence to allocate.

## Cross-References

- `corvus-builder-context-threading` — write UTF-8 spans into a generated model with no closure (the `Build<TContext>` form).
- `corvus-buffer-and-pooling` — the `stackalloc`/`ArrayPool`/thread-local pooling the span paths rent from.
- `corvus-typed-model-construction` — constructing generated models from native values (the non-span common case).
- `ref-struct-delegates` — why the build callbacks are named `ref struct` delegates, not `Func<>`/`Action<>`.
- `corvus-parsed-documents-and-memory` — `GetUtf8String()` / `TakeOwnership` / the deferred-holder memory model.
