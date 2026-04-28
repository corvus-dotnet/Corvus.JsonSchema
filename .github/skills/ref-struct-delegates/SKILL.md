---
name: ref-struct-delegates
description: >
  Replace Func<> and Action<> with custom delegate types when any parameter or return
  type is a ref struct (Span<T>, ReadOnlySpan<T>, etc.). Ref structs cannot appear as
  generic type arguments, so Func<ReadOnlySpan<byte>, bool> is a compile error. The fix
  is a named delegate. USE FOR: writing public APIs that accept callbacks involving spans,
  storing span-accepting lambdas in fields, wrapping IJsonPathFunction or similar interfaces
  with delegate-based factories. DO NOT USE FOR: APIs where all parameters are regular
  (non-ref-struct) types — plain Func<>/Action<> is fine there.
---

# Ref Struct Delegate Translation

## The Problem

`Func<>`, `Action<>`, and `Predicate<>` are generic types. C# forbids ref structs
(`Span<T>`, `ReadOnlySpan<T>`, `ReadOnlyMemory<T>.Span`, etc.) as generic type
arguments. This means:

```csharp
// ❌ COMPILE ERROR — ReadOnlySpan<T> is a ref struct
Func<ReadOnlySpan<byte>, bool> predicate;
Action<Span<char>> writer;
Func<ReadOnlySpan<JsonPathFunctionArgument>, JsonWorkspace, JsonPathFunctionResult> evaluator;
```

## The Fix: Named Delegates

Define a named delegate type with the ref struct parameter directly:

```csharp
// ✅ COMPILES — named delegates can have ref struct parameters
public delegate bool SpanPredicate(ReadOnlySpan<byte> data);
public delegate void SpanWriter(Span<char> buffer);
public delegate JsonPathFunctionResult JsonPathFunctionEvaluator(
    ReadOnlySpan<JsonPathFunctionArgument> arguments,
    JsonWorkspace workspace);
```

## Translation Rules

### 1. Identify the offending Func/Action

Find every `Func<>` or `Action<>` where **any** type argument is a ref struct.

### 2. Create a named delegate with the same shape

| Original (broken) | Replacement |
|---|---|
| `Func<ReadOnlySpan<T>, TResult>` | `delegate TResult MyDelegate(ReadOnlySpan<T> items);` |
| `Func<ReadOnlySpan<T>, TOther, TResult>` | `delegate TResult MyDelegate(ReadOnlySpan<T> items, TOther other);` |
| `Action<Span<T>>` | `delegate void MyDelegate(Span<T> buffer);` |
| `Predicate<ReadOnlySpan<T>>` | `delegate bool MyDelegate(ReadOnlySpan<T> items);` |

### 3. Replace all usages

Fields, parameters, locals, and lambda assignments all change from the `Func<>`/`Action<>` type to the named delegate. Lambda syntax remains identical — only the declared type changes:

```csharp
// Before (broken):
Func<ReadOnlySpan<byte>, int, bool> check = (span, len) => span.Length >= len;

// After (works):
public delegate bool SpanCheck(ReadOnlySpan<byte> span, int minLength);
SpanCheck check = (span, len) => span.Length >= len;
```

## Convenience Factory Pattern

When building factory methods that wrap lambdas into an interface (e.g. `IJsonPathFunction`),
split the API into two tiers:

1. **Convenience overloads** — use `Func<>` with non-ref-struct projections of the data
   (e.g. copy `ReadOnlySpan<T>` to `T[]` before passing to the delegate):

```csharp
// Nodes are copied to an array so Func<> works
public static IJsonPathFunction NodesValue(
    Func<JsonElement[], JsonWorkspace, JsonElement> func)
    => new DelegateFunction(
        JsonPathFunctionType.ValueType,
        [JsonPathFunctionType.NodesType],
        (args, ws) => JsonPathFunctionResult.FromValue(
            func(args[0].Nodes.ToArray(), ws)));
```

2. **Full-control overload** — use the named delegate for zero-copy access:

```csharp
public delegate JsonPathFunctionResult JsonPathFunctionEvaluator(
    ReadOnlySpan<JsonPathFunctionArgument> arguments,
    JsonWorkspace workspace);

public static IJsonPathFunction Create(
    JsonPathFunctionType returnType,
    JsonPathFunctionType[] parameterTypes,
    JsonPathFunctionEvaluator evaluate)
    => new DelegateFunction(returnType, parameterTypes, evaluate);
```

This gives callers a choice: simple lambda with a small allocation (`Func<T[], ...>`)
or zero-allocation with the named delegate.

## Storing the Delegate in a Field

Named delegates are regular reference types and can be stored in fields, unlike the
ref structs they accept:

```csharp
private sealed class DelegateFunction : IJsonPathFunction
{
    private readonly JsonPathFunctionEvaluator evaluate; // ✅ field storage works

    public DelegateFunction(JsonPathFunctionEvaluator evaluate)
    {
        this.evaluate = evaluate;
    }

    public JsonPathFunctionResult Evaluate(
        ReadOnlySpan<JsonPathFunctionArgument> arguments,
        JsonWorkspace workspace)
        => this.evaluate(arguments, workspace);
}
```

## Naming Convention

Name the delegate after its purpose, not its shape. Good names describe *what* the
callback does:

| ✅ Good | ❌ Bad |
|---|---|
| `JsonPathFunctionEvaluator` | `SpanFuncDelegate` |
| `SpanPredicate` | `ReadOnlySpanBoolFunc` |
| `BufferWriter` | `SpanAction` |

## Scope

This applies to **all** ref struct types, not just spans:

- `Span<T>`, `ReadOnlySpan<T>`
- Custom `ref struct` types in the codebase
- Any future `ref struct` added by .NET

## Cross-References

- For the stackalloc/ArrayPool rent pattern that produces spans, see `corvus-buffer-and-pooling`
- For ref-struct collections in this codebase, see `corvus-low-alloc-data-structures`
