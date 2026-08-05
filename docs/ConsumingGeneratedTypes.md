# Consuming Generated Types

## Overview

Corvus generates strongly-typed .NET models from JSON Schema, OpenAPI, and AsyncAPI (through the
[source generator](./SourceGenerator.md), the [CLI](./CodeGenerator.md), and the runtime
[Validator](./Validator.md)). This guide is about reading and building those models in your own code, allocation
free, without composing or parsing JSON strings by hand. The conventions below differ from `System.Text.Json` in
a few ways that are worth knowing up front.

## The types are Corvus's, not System.Text.Json's

A generated model and every value you read from it are `Corvus.Text.Json` types. `JsonElement`, `JsonValueKind`,
and the typed accessors are Corvus's, operating on UTF-8 bytes. Reach for the Corvus members below rather than a
`System.Text.Json` API of the same name.

## Optional properties are Undefined, not null

An absent optional property returns an **Undefined** value, not `null`. A generated value is a non-nullable
struct, so testing it against `null` never fires. Check with `IsNotUndefined()`, then convert to the native type.

```csharp
string? baseWorkflowId = body.BaseWorkflowId.IsNotUndefined() ? (string)body.BaseWorkflowId : null;
long? seconds = body.RequestedDurationSeconds.IsNotUndefined() ? (long)body.RequestedDurationSeconds : null;
```

## Reading values

Convert a scalar to its native type with a cast: `(string)value`, `(long)value`, `(int)value`, `(bool)value`.
For a date or date-time, use the typed accessor rather than a cast plus `DateTimeOffset.Parse`, which is not
JSON-Schema compliant.

```csharp
DateTimeOffset when = element.GetDateTimeOffset();
```

To read a string value's bytes without allocating a managed `string`, use `element.GetUtf8String()` (see the UTF-8
section of [Performance Techniques](./PerformanceTechniques.md)).

### Discriminated unions: `Match`

A `oneOf` schema generates a union. Read it with the generated `Match<TResult>(...)` (or the `IsX` / `AsX`
accessors) rather than inspecting `ValueKind` by hand, so the compiler checks that you handled every case.

## An absent optional object property throws on nested access

An absent optional **object** property returns a default value whose parent is null, so navigating into it throws
a `NullReferenceException`. Gate nested access on the value's kind, which covers both the absent case and an
explicit JSON `null`.

```csharp
if (parent.Child.ValueKind == JsonValueKind.Object)
{
    // safe to read parent.Child.GrandChild here
}
```

## Schema defaults materialise in the getter

If a property declares a non-null `default` in its schema, the generated getter returns that default when the
property is absent. So express a server-assigned or optional-with-a-default value in the schema rather than
hand-writing a null guard at every call site; the generated type carries the default for you.

## Building a value: convert and construct, never compose-then-parse

Do not build a body as a JSON string and `ParseValue` it (an interim string, a parse, and a re-validation).
Convert a native value with the static `From` factory, and build a model with its generated factories.

```csharp
JsonString token = JsonString.From(rawPageToken);                     // native value to a generated type
await client.ResumeRunAsync(runId, RewindResume.Build(targetCursor), ct);  // build a body, hand it straight over
```

Pick the factory by situation.

| Factory | Returns | Use when |
|---------|---------|----------|
| `T.Build(field: v, …)` | `T.Source` (lazy) | The default. Native values, including a `JsonElement` or array, passed straight in. Hand the `Source` to a consumer (a generated client or handler) that materialises it once. No workspace, no closure. |
| `T.Build(static (ref T.Builder b) => …)` | `T.Source` (lazy) | No fields to set, or you need imperative logic. The lambda must be `static`. |
| `T.CreateBuilder(ws, field: v, …).RootElement` | `T` (materialised) | Only when you need a materialised value in hand (to read it back or store it), not merely to pass it on. |
| `T.CreateBuilder<TContext>(ws, ctx, static (in TContext ctx, ref T.Builder b) => …).RootElement` | `T` | Materialising while threading runtime values into a builder loop or conditional, closure free. |

For a discriminated union, build the **variant**, not the union; the variant's `Source` converts implicitly to the
union's, so you pass the builder straight to the consumer with no materialisation. A `const`-discriminated variant
sets its own discriminator inside `Build`, so you never set it yourself.

### The `JsonElement.Source` ref-safety trap

`JsonElement.Source` is a `ref struct` created from an `in JsonElement`; it holds a ref to the element. Passing a
`JsonElement` **directly as an argument** to a `Build(in …)` or `CreateBuilder(in …)` factory is fine (the call's
scope keeps the source document alive). What fails is **capturing** a `JsonElement` in a non-static builder lambda,
where the captured ref escapes into the closure and the compiler rejects it (CS8168 / CS8347 / CS8350).

```csharp
// wrong: outputs is captured; its ref escapes the closure
JsonElement outputs = doc.RootElement;
var src = new SkipResume.Source((ref SkipResume.Builder b) => b.Create(skipOutputs: outputs));

// right: pass it to the native-field factory, no lambda, no capture
var src = SkipResume.Build(skipOutputs: doc.RootElement);
```

If you genuinely need imperative building and a threaded `JsonElement`, use the `CreateBuilder<TContext>` form so
the value flows through `scoped in` rather than a closure. Always make builder lambdas `static` and thread state
through the context tuple; a non-static lambda allocates a closure and reintroduces the capture problem.

## Cross-assembly type identity

Each generation root emits its own copy of the primitive types, so there is no single shared `JsonString`. When a
generated type crosses an assembly boundary (a public seam typed on it), reference it by its fully-qualified name;
the same short name from another assembly is a distinct type. See the
[source generator troubleshooting](./SourceGenerator.md#troubleshooting).

## Lifetime

A materialised value from `.RootElement` is a view over its `JsonWorkspace`, and a lazy `.Source` references
whatever it was built from. Keep the `JsonWorkspace` or source `ParsedJsonDocument` alive until the consumer has
finished writing the value, for example across the `await` on a client call. `JsonWorkspace`,
`JsonDocumentBuilder<T>`, and `ParsedJsonDocument<T>` are all `using`-disposable.

## See also

- [Source Generator Code Generation](./SourceGenerator.md) and [CLI Code Generation](./CodeGenerator.md) for
  generating the types.
- [Parsing & Reading JSON](./ParsedJsonDocument.md) for the read-only document model.
- [Building & Mutating JSON](./JsonDocumentBuilder.md) for building and modifying arbitrary documents.
- [Performance Techniques](./PerformanceTechniques.md) for the UTF-8 and allocation model underneath.
